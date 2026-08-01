namespace AI_Dashboard.Application.Common.Schema;

public class ColumnMetadata
{
    public string ColumnName  { get; set; } = default!;
    public string DataType    { get; set; } = default!;
    public bool   IsNullable  { get; set; }
    public bool   IsPrimaryKey { get; set; }
}
