using AI_Dashboard.Domain.Common;
 
namespace AI_Dashboard.Domain.Entities;
 
public class DashboardFilter : BaseEntity
{
   public int DashboardId { get; set; }
   public Dashboard? Dashboard { get; set; }
 
   public int? WidgetId { get; set; }
   public Widget? Widget { get; set; }
 
   public string FieldName { get; set; } = string.Empty;
   public string Operator { get; set; } = "=";
   public string? Value { get; set; }
   public string FilterType { get; set; } = "text";
}
 
