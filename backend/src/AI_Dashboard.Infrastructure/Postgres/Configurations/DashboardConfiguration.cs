using AI_Dashboard.Domain.Entities;//TABLE MAPPING FILE
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI_Dashboard.Infrastructure.Postgres.Configurations;

public class DashboardConfiguration : IEntityTypeConfiguration<Dashboard>
{
    public void Configure(EntityTypeBuilder<Dashboard> b)
    {
        b.ToTable("dashboards");
        b.HasKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);

        b.Property(x => x.Visibility)
            .HasColumnName("visibility")
            .HasMaxLength(20);

        b.Property(x => x.DashboardStyle)
            .HasColumnName("dashboard_style")
            .HasMaxLength(50);

        b.Property(x => x.IsDefault).HasColumnName("is_default");
        b.Property(x => x.VersionNo).HasColumnName("version_no");
        b.Property(x => x.DefinitionJson).HasColumnName("definition").HasColumnType("jsonb");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasOne(x => x.User)
            .WithMany(u => u.Dashboards)
            .HasForeignKey(x => new { x.TenantId, x.UserId })
            .HasPrincipalKey(u => new { u.TenantId, u.Id });

        b.HasIndex(x => new { x.TenantId, x.UserId })
            .HasFilter("is_default = true")
            .IsUnique();
    }
}
