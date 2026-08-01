using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Common.Schema;
using Npgsql;
using System.Text;

namespace AI_Dashboard.Infrastructure.SchemaIntrospection;

public class PostgresSchemaIntrospectionService : ISchemaIntrospectionService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly SchemaCache _cache;
    private readonly string[] _allowedTables;
    private readonly HashSet<string> _excludedTables;
    private readonly string? _tenantColumn;

    public PostgresSchemaIntrospectionService(NpgsqlDataSource dataSource, SchemaCache cache, ISchemaIntrospectionOptions options)
    {
        _dataSource = dataSource;
        _cache = cache;
        _allowedTables = options.AllowedTables;
        _excludedTables = new HashSet<string>(options.ExcludedTables, StringComparer.OrdinalIgnoreCase);
        _tenantColumn = options.TenantColumn;
    }

    public async Task<string> GetSchemaDescriptionAsync(short tenantId, CancellationToken ct = default)
    {
        var tables = await GetTablesAsync(tenantId, ct);
        var sb = new StringBuilder();
        foreach (var t in tables)
        {
            sb.Append(t.TableName).Append('(');
            sb.Append(string.Join(", ", t.Columns.Select(c =>
                c.IsNullable ? $"{c.ColumnName} {c.DataType}?" : $"{c.ColumnName} {c.DataType}")));
            sb.Append(")\n");
        }
        return sb.ToString();
    }

    public async Task<List<TableMetadata>> GetTablesAsync(short tenantId, CancellationToken ct = default)
    {
        var cached = _cache.Get(tenantId);
        if (cached is not null) return cached;
        var tables = await LoadSchemaAsync(tenantId, ct);
        _cache.Set(tenantId, tables);
        return tables;
    }

    private async Task<List<string>> DiscoverTablesAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var names = new List<string>();
        while (await r.ReadAsync(ct))
        {
            var name = r.GetString(0);
            if (!_excludedTables.Contains(name))
                names.Add(name);
        }
        return names;
    }

    private async Task<List<TableMetadata>> LoadSchemaAsync(short tenantId, CancellationToken ct)
    {
        var tables = new List<TableMetadata>();
        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        var tableNames = _allowedTables.Length > 0
            ? _allowedTables
            : (await DiscoverTablesAsync(conn, ct)).ToArray();

        foreach (var tableName in tableNames)
        {
            var table = new TableMetadata { TableName = tableName };

            const string colSql = """
                SELECT c.column_name, c.data_type, c.is_nullable,
                       CASE WHEN kcu.column_name IS NOT NULL THEN true ELSE false END AS is_pk
                FROM information_schema.columns c
                LEFT JOIN information_schema.table_constraints tc
                    ON tc.table_name = c.table_name AND tc.table_schema = c.table_schema
                    AND tc.constraint_type = 'PRIMARY KEY'
                LEFT JOIN information_schema.key_column_usage kcu
                    ON kcu.constraint_name = tc.constraint_name
                    AND kcu.column_name = c.column_name
                WHERE c.table_name = @table AND c.table_schema = 'public'
                ORDER BY c.ordinal_position
            """;
            await using (var cmd = new NpgsqlCommand(colSql, conn))
            {
                cmd.Parameters.AddWithValue("table", tableName);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    table.Columns.Add(new ColumnMetadata
                    {
                        ColumnName   = r.GetString(0),
                        DataType     = r.GetString(1),
                        IsNullable   = r.GetString(2) == "YES",
                        IsPrimaryKey = r.GetBoolean(3)
                    });
            }

            const string purposeSql = """
                SELECT purpose FROM schema_chunks
                WHERE tenant_id = @tenantId AND @table = ANY(tables) AND purpose IS NOT NULL
                LIMIT 1
            """;
            await using (var cmd = new NpgsqlCommand(purposeSql, conn))
            {
                cmd.Parameters.AddWithValue("tenantId", tenantId);
                cmd.Parameters.AddWithValue("table", tableName);
                var result = await cmd.ExecuteScalarAsync(ct);
                table.Purpose = result as string;
            }

            // NOTE: information_schema.key_column_usage joined to constraint_column_usage
            // ONLY on constraint_name produces a cross product for composite FKs (our
            // tables all use (tenant_id, x) -> (tenant_id, id) composite keys), yielding
            // spurious pairs like tenant_id -> agents.id. pg_constraint's conkey/confkey
            // arrays preserve column ORDER, so unnesting them together (positionally)
            // gives the correct 1:1 column pairing regardless of key width.
            const string fkSql = """
                SELECT
                    att_from.attname  AS from_column,
                    cl_to.relname     AS to_table,
                    att_to.attname    AS to_column
                FROM pg_constraint con
                JOIN pg_class cl_from ON cl_from.oid = con.conrelid
                JOIN pg_class cl_to   ON cl_to.oid   = con.confrelid
                JOIN LATERAL unnest(con.conkey, con.confkey)
                    WITH ORDINALITY AS cols(from_attnum, to_attnum, ord) ON true
                JOIN pg_attribute att_from
                    ON att_from.attrelid = con.conrelid AND att_from.attnum = cols.from_attnum
                JOIN pg_attribute att_to
                    ON att_to.attrelid = con.confrelid AND att_to.attnum = cols.to_attnum
                WHERE con.contype = 'f'
                  AND cl_from.relname = @table
                  AND cl_from.relnamespace = 'public'::regnamespace
                ORDER BY con.conname, cols.ord
            """;
            await using (var cmd = new NpgsqlCommand(fkSql, conn))
            {
                cmd.Parameters.AddWithValue("table", tableName);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var fromColumn = r.GetString(0);
                    var toTable    = r.GetString(1);
                    var toColumn   = r.GetString(2);

                    // Skip the tenant column leg of composite FKs. It's a valid, correctly-
                    // paired edge now (see fkSql fix above), but it's tenant scoping, not a
                    // real join key — TenantInjectionStep already enforces it separately.
                    // Leaving it in the graph risks FindJoinPath's BFS picking tenant_id as
                    // the join column over the actual semantic FK (e.g. agent_id -> agents.id).
                    if (_tenantColumn is not null
                        && fromColumn.Equals(_tenantColumn, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!_excludedTables.Contains(toTable))
                        table.Relationships.Add(new RelationshipMetadata
                        {
                            FromColumn = fromColumn,
                            ToTable    = toTable,
                            ToColumn   = toColumn
                        });
                }
            }

            tables.Add(table);
        }
        return tables;
    }

    public async Task<List<string>> GetDistinctValuesAsync(short tenantId, string table, string column, CancellationToken ct = default)
    {
        if (_excludedTables.Contains(table)) return [];
        var effectiveTables = _allowedTables.Length > 0 ? _allowedTables : null;
        if (effectiveTables is not null && !effectiveTables.Contains(table, StringComparer.OrdinalIgnoreCase))
            return [];

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        const string existsSql = """
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND column_name = @col
        """;
        await using (var chk = new NpgsqlCommand(existsSql, conn))
        {
            chk.Parameters.AddWithValue("table", table);
            chk.Parameters.AddWithValue("col", column);
            if (await chk.ExecuteScalarAsync(ct) is null) return [];
        }

        const string hasTenantSql = """
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND column_name = 'tenant_id'
        """;
        bool hasTenant;
        await using (var tc = new NpgsqlCommand(hasTenantSql, conn))
        {
            tc.Parameters.AddWithValue("table", table);
            hasTenant = await tc.ExecuteScalarAsync(ct) is not null;
        }

        var sql = hasTenant
            ? $"SELECT DISTINCT {column} FROM {table} WHERE tenant_id = @tenantId AND {column} IS NOT NULL ORDER BY 1 LIMIT 50"
            : $"SELECT DISTINCT {column} FROM {table} WHERE {column} IS NOT NULL ORDER BY 1 LIMIT 50";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (hasTenant) cmd.Parameters.AddWithValue("tenantId", tenantId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var values = new List<string>();
        while (await r.ReadAsync(ct))
            values.Add(r.GetString(0));
        return values;
    }
}