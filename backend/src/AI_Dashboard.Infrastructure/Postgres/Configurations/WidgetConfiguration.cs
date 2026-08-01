using AI_Dashboard.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI_Dashboard.Infrastructure.Postgres.Configurations;

public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
{
    public void Configure(EntityTypeBuilder<Widget> b)
    {
        b.ToTable("widgets");
        b.HasKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(100);

       b.Property(x => x.WidgetType).HasColumnName("widget_type").HasMaxLength(50);

        b.Property(x => x.Width).HasColumnName("width");
        b.Property(x => x.Height).HasColumnName("height");
        b.Property(x => x.PositionX).HasColumnName("position_x");
        b.Property(x => x.PositionY).HasColumnName("position_y");
        b.Property(x => x.RefreshInterval).HasColumnName("refresh_interval");
        b.Property(x => x.DataSource).HasColumnName("data_source").HasMaxLength(50);
        b.Property(x => x.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
        b.Property(x => x.DashboardId).HasColumnName("dashboard_id");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasOne(x => x.Dashboard)
            .WithMany(d => d.Widgets)
            .HasForeignKey(x => new { x.TenantId, x.DashboardId })
            .HasPrincipalKey(d => new { d.TenantId, d.Id });
    }
}