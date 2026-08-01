using AI_Dashboard.Application.Common.Schema;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface ISchemaGraph
{
    void Build(List<TableMetadata> tables);
    List<string>? FindJoinPath(string fromTable, string toTable, int maxHops = 5);
}
