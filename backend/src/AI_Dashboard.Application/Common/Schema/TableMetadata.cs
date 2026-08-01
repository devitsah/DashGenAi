namespace AI_Dashboard.Application.Common.Schema;

public class TableMetadata
{
    public string TableName { get; set; } = default!;
    public string? Purpose { get; set; }
    public List<ColumnMetadata>      Columns       { get; set; } = new();
    public List<RelationshipMetadata> Relationships { get; set; } = new();
}
