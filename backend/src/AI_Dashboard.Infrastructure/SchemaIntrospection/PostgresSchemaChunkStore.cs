using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Common.Schema;
using Npgsql;
using Pgvector;

namespace AI_Dashboard.Infrastructure.SchemaIntrospection;

public class PostgresSchemaChunkStore : ISchemaChunkStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOllamaClient _ollama;
    private readonly ISchemaIntrospectionOptions _options;

    public PostgresSchemaChunkStore(NpgsqlDataSource dataSource, IOllamaClient ollama, ISchemaIntrospectionOptions options)
    {
        _dataSource = dataSource;
        _ollama = ollama;
        _options = options;
    }

    public async Task ClearAsync(short tenantId, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        foreach (var table in new[] { "schema_chunks", "relationship_chunks", "metadata_catalog" })
        {
            await using var cmd = new NpgsqlCommand($"DELETE FROM {table} WHERE tenant_id = @t", conn);
            cmd.Parameters.AddWithValue("t", tenantId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<bool> HasChunksAsync(short tenantId, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        const string sql = "SELECT EXISTS(SELECT 1 FROM schema_chunks WHERE tenant_id = @t)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("t", tenantId);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task RebuildAsync(short tenantId, List<TableMetadata> tables, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        foreach (var table in tables)
        {
            var text = BuildTableDescription(table);
            var hash = ComputeHash(text);
            if (await HashUnchangedAsync(conn, tenantId, table.TableName, hash, ct)) continue;
            var vec = await _ollama.EmbedAsync(text, ct);
            await UpsertSchemaChunkAsync(conn, tenantId, table.TableName,
                new[] { table.TableName }, text, table.Purpose, hash, vec, ct);
        }
    }

    public async Task<List<TableMetadata>> RetrieveRelevantTablesAsync(
        short tenantId, string queryText, List<TableMetadata> allTables, int topK = 2, CancellationToken ct = default)
    {
        var vector = new Vector(await _ollama.EmbedAsync(queryText, ct));
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        // Return the distance itself so we can reject anything that's not actually
        // relevant — without this, LIMIT always returns topK rows regardless of how
        // far away they are, which is exactly why off-domain prompts like "Book a
        // movie ticket" or "Show planets" always matched *some* table before.
        const string sql = """
            SELECT tables, embedding <=> @qvec AS distance
            FROM schema_chunks
            WHERE tenant_id = @tenantId
            ORDER BY embedding <=> @qvec
            LIMIT @topK
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("qvec", vector);
        cmd.Parameters.AddWithValue("topK", topK);

        var relevantNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                var distance = r.GetDouble(1);
                if (distance > _options.SchemaRelevanceMaxDistance) continue;
                foreach (var name in (string[])r[0])
                    relevantNames.Add(name);
            }

        // No blind fallback to allTables — an empty result here is a real signal
        // ("this prompt isn't about anything in this schema") that callers need to
        // surface, not paper over by handing back every table in the database.
        return allTables.Where(t => relevantNames.Contains(t.TableName)).ToList();
    }

    public async Task RebuildRelationshipsAsync(short tenantId, List<TableMetadata> tables, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        foreach (var t in tables)
        {
            foreach (var r in t.Relationships)
            {
                var text      = $"{t.TableName}.{r.FromColumn} joins {r.ToTable}.{r.ToColumn}";
                var hash      = ComputeHash(text);
                var chunkName = $"{t.TableName}_{r.FromColumn}_{r.ToTable}";
                if (await RelHashUnchangedAsync(conn, tenantId, chunkName, hash, ct)) continue;
                var vec = await _ollama.EmbedAsync(text, ct);
                const string upsert = """
                    INSERT INTO relationship_chunks
                        (tenant_id, chunk_name, from_table, to_table, relationship_text, schema_hash, embedding, updated_at)
                    VALUES (@t, @n, @ft, @tt, @txt, @hash, @emb, NOW())
                    ON CONFLICT (tenant_id, chunk_name)
                    DO UPDATE SET from_table=@ft, to_table=@tt, relationship_text=@txt,
                                  schema_hash=@hash, embedding=@emb, updated_at=NOW()
                """;
                await using var cmd = new NpgsqlCommand(upsert, conn);
                cmd.Parameters.AddWithValue("t",    tenantId);
                cmd.Parameters.AddWithValue("n",    chunkName);
                cmd.Parameters.AddWithValue("ft",   t.TableName);
                cmd.Parameters.AddWithValue("tt",   r.ToTable);
                cmd.Parameters.AddWithValue("txt",  text);
                cmd.Parameters.AddWithValue("hash", hash);
                cmd.Parameters.AddWithValue("emb",  new Vector(vec));
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
    }

    public async Task<List<RelationshipChunk>> RetrieveRelevantRelationshipsAsync(
        short tenantId, string queryText, int topK = 3, CancellationToken ct = default)
    {
        var vec = new Vector(await _ollama.EmbedAsync(queryText, ct));
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT from_table, to_table, relationship_text
            FROM relationship_chunks
            WHERE tenant_id = @tenantId
            ORDER BY embedding <=> @qvec
            LIMIT @topK
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("qvec",     vec);
        cmd.Parameters.AddWithValue("topK",     topK);
        var result = new List<RelationshipChunk>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new RelationshipChunk(r.GetString(0), r.GetString(1), r.GetString(2)));
        return result;
    }

    public async Task RebuildMetadataCatalogAsync(short tenantId, List<TableMetadata> tables,
        Func<string, string, Task<List<string>>> getSampleValues, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        foreach (var t in tables)
        {
            foreach (var c in t.Columns)
            {
                List<string> samples;
                try   { samples = await getSampleValues(t.TableName, c.ColumnName); }
                catch { samples = []; }
                if (samples.Count == 0) continue;

                var samplesJson = JsonSerializer.Serialize(samples);
                var embedText = $"{t.TableName} {c.ColumnName} {c.DataType} values: {string.Join(" ", samples)}";
                var hash = ComputeHash(samplesJson);
                if (await MetaHashUnchangedAsync(conn, tenantId, t.TableName, c.ColumnName, hash, ct)) continue;

                var vec = await _ollama.EmbedAsync(embedText, ct);
                const string upsert = """
                    INSERT INTO metadata_catalog
                        (tenant_id, table_name, column_name, sample_values, data_type, schema_hash, embedding, updated_at)
                    VALUES (@t, @tn, @cn, @sv, @dt, @hash, @emb, NOW())
                    ON CONFLICT (tenant_id, table_name, column_name)
                    DO UPDATE SET sample_values=@sv, data_type=@dt, schema_hash=@hash,
                                  embedding=@emb, updated_at=NOW()
                """;
                await using var cmd = new NpgsqlCommand(upsert, conn);
                cmd.Parameters.AddWithValue("t",    tenantId);
                cmd.Parameters.AddWithValue("tn",   t.TableName);
                cmd.Parameters.AddWithValue("cn",   c.ColumnName);
                cmd.Parameters.AddWithValue("sv",   samplesJson);
                cmd.Parameters.AddWithValue("dt",   c.DataType);
                cmd.Parameters.AddWithValue("hash", hash);
                cmd.Parameters.AddWithValue("emb",  new Vector(vec));
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
    }

    public async Task<List<MetadataEntry>> RetrieveRelevantMetadataAsync(
        short tenantId, string queryText, int topK = 5, CancellationToken ct = default)
    {
        var vec = new Vector(await _ollama.EmbedAsync(queryText, ct));
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT table_name, column_name, data_type, sample_values
            FROM metadata_catalog
            WHERE tenant_id = @tenantId
            ORDER BY embedding <=> @qvec
            LIMIT @topK
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("qvec",     vec);
        cmd.Parameters.AddWithValue("topK",     topK);
        var result = new List<MetadataEntry>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            List<string> samples;
            try { samples = JsonSerializer.Deserialize<List<string>>(r.GetString(3)) ?? []; }
            catch { samples = []; }
            result.Add(new MetadataEntry(r.GetString(0), r.GetString(1), r.GetString(2), samples));
        }
        return result;
    }

    private static string BuildTableDescription(TableMetadata t)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Table: {t.TableName}");
        if (!string.IsNullOrWhiteSpace(t.Purpose))
            sb.AppendLine($"Purpose: {t.Purpose}");
        sb.AppendLine("Columns: " + string.Join(", ", t.Columns.Select(c =>
            $"{c.ColumnName} ({c.DataType})")));
        foreach (var r in t.Relationships)
            sb.AppendLine($"FK: {t.TableName}.{r.FromColumn} -> {r.ToTable}.{r.ToColumn}");
        return sb.ToString();
    }

    private static string ComputeHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static async Task<bool> HashUnchangedAsync(
        NpgsqlConnection conn, short tenantId, string chunkName, string hash, CancellationToken ct)
    {
        const string sql = "SELECT schema_hash FROM schema_chunks WHERE tenant_id = @t AND chunk_name = @n";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("n", chunkName);
        return (await cmd.ExecuteScalarAsync(ct) as string) == hash;
    }

    private static async Task<bool> RelHashUnchangedAsync(
        NpgsqlConnection conn, short tenantId, string chunkName, string hash, CancellationToken ct)
    {
        const string sql = "SELECT schema_hash FROM relationship_chunks WHERE tenant_id = @t AND chunk_name = @n";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("n", chunkName);
        return (await cmd.ExecuteScalarAsync(ct) as string) == hash;
    }

    private static async Task<bool> MetaHashUnchangedAsync(
        NpgsqlConnection conn, short tenantId, string table, string column, string hash, CancellationToken ct)
    {
        const string sql = "SELECT schema_hash FROM metadata_catalog WHERE tenant_id=@t AND table_name=@tn AND column_name=@cn";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("t",  tenantId);
        cmd.Parameters.AddWithValue("tn", table);
        cmd.Parameters.AddWithValue("cn", column);
        return (await cmd.ExecuteScalarAsync(ct) as string) == hash;
    }

    private static async Task UpsertSchemaChunkAsync(
        NpgsqlConnection conn, short tenantId, string chunkName, string[] tableNames,
        string text, string? purpose, string hash, float[] embedding, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO schema_chunks
                (tenant_id, chunk_name, tables, chunk_text, purpose, schema_hash, embedding, updated_at)
            VALUES (@t, @n, @tables, @text, @purpose, @hash, @emb, NOW())
            ON CONFLICT (tenant_id, chunk_name)
            DO UPDATE SET tables=@tables, chunk_text=@text, purpose=@purpose,
                          schema_hash=@hash, embedding=@emb, updated_at=NOW()
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("t",       tenantId);
        cmd.Parameters.AddWithValue("n",       chunkName);
        cmd.Parameters.AddWithValue("tables",  tableNames);
        cmd.Parameters.AddWithValue("text",    text);
        cmd.Parameters.AddWithValue("purpose", (object?)purpose ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hash",    hash);
        cmd.Parameters.AddWithValue("emb",     new Vector(embedding));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}