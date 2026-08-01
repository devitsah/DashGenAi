using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI_Dashboard.Infrastructure.Postgres.Configurations;

public class QueryConfiguration : IEntityTypeConfiguration<Query>
{
    public void Configure(EntityTypeBuilder<Query> b)
    {
        b.ToTable("queries");
        b.HasKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.SqlQuery).HasColumnName("sql_query");
        b.Property(x => x.DashboardId).HasColumnName("dashboard_id");
        b.Property(x => x.WidgetId).HasColumnName("widget_id");
        b.Property(x => x.PromptId).HasColumnName("prompt_id");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasOne<Widget>()
            .WithMany(w => w.Queries)
            .HasForeignKey(nameof(Query.TenantId), nameof(Query.WidgetId))
            .HasPrincipalKey(nameof(Widget.TenantId), nameof(Widget.Id));
    }
}