namespace AI_Dashboard.Application.Common.Schema;

public class RelationshipMetadata
{
    public string FromColumn { get; set; } = default!;
    public string ToTable    { get; set; } = default!;
    public string ToColumn   { get; set; } = default!;
}
