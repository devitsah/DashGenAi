using AI_Dashboard.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AI_Dashboard.Infrastructure.SchemaIntrospection;

public class SchemaPreWarmService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISchemaIntrospectionOptions _options;
    private readonly ILogger<SchemaPreWarmService> _logger;

    public SchemaPreWarmService(
        IServiceScopeFactory scopeFactory,
        ISchemaIntrospectionOptions options,
        ILogger<SchemaPreWarmService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        if (_options.WarmupTenantIds.Length == 0) return Task.CompletedTask;
        // Run in background so app starts immediately and serves login/register
        // without waiting for all embedding calls to complete.
        _ = Task.Run(() => RunWarmupAsync(ct), ct);
        return Task.CompletedTask;
    }

    private async Task RunWarmupAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var schemaService = scope.ServiceProvider.GetRequiredService<ISchemaIntrospectionService>();
        var chunkStore    = scope.ServiceProvider.GetRequiredService<ISchemaChunkStore>();
        var schemaGraph   = scope.ServiceProvider.GetRequiredService<ISchemaGraph>();

        foreach (var tenantId in _options.WarmupTenantIds)
        {
            try
            {
                _logger.LogInformation("Pre-warming schema for tenant {TenantId}", tenantId);
                var tables = await schemaService.GetTablesAsync(tenantId, ct);
                schemaGraph.Build(tables);
                await chunkStore.ClearAsync(tenantId, ct);
                await Task.WhenAll(
                    chunkStore.RebuildAsync(tenantId, tables, ct),
                    chunkStore.RebuildRelationshipsAsync(tenantId, tables, ct),
                    chunkStore.RebuildMetadataCatalogAsync(tenantId, tables,
                        (tbl, col) => schemaService.GetDistinctValuesAsync(tenantId, tbl, col, ct), ct)
                );
                _logger.LogInformation("Schema pre-warm complete for tenant {TenantId}", tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schema pre-warm failed for tenant {TenantId}", tenantId);
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
