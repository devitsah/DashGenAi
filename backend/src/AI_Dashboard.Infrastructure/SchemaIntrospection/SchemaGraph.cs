using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Common.Schema;

namespace AI_Dashboard.Infrastructure.SchemaIntrospection;

public class SchemaGraph : ISchemaGraph
{
    private volatile Dictionary<string, List<(string FromCol, string ToTable, string ToCol)>> _edges = new();

    public void Build(List<TableMetadata> tables)
    {
        var edges = new Dictionary<string, List<(string, string, string)>>();
        foreach (var t in tables)
        {
            edges.TryAdd(t.TableName, new());
            foreach (var r in t.Relationships)
            {
                edges[t.TableName].Add((r.FromColumn, r.ToTable, r.ToColumn));
                edges.TryAdd(r.ToTable, new());
                edges[r.ToTable].Add((r.ToColumn, t.TableName, r.FromColumn));
            }
        }
        // Atomic swap — safe for concurrent readers on the singleton
        System.Threading.Interlocked.Exchange(ref _edges!, edges);
    }

    public List<string>? FindJoinPath(string fromTable, string toTable, int maxHops = 5)
    {
        if (fromTable == toTable) return new();
        if (!_edges.ContainsKey(fromTable)) return null;

        var visited = new HashSet<string> { fromTable };
        var queue = new Queue<(string Table, int Depth, List<string> Path)>();
        queue.Enqueue((fromTable, 0, new()));

        while (queue.Count > 0)
        {
            var (current, depth, path) = queue.Dequeue();
            if (depth >= maxHops) continue;
            if (!_edges.TryGetValue(current, out var neighbors)) continue;

            foreach (var (fromCol, neighborTable, toCol) in neighbors)
            {
                if (!visited.Add(neighborTable)) continue;
                var clause = $"JOIN {neighborTable} ON {current}.{fromCol} = {neighborTable}.{toCol}";
                var newPath = new List<string>(path) { clause };
                if (neighborTable == toTable) return newPath;
                queue.Enqueue((neighborTable, depth + 1, newPath));
            }
        }
        return null;
    }
}
