using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI_Dashboard.Infrastructure.Postgres.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(50);
        b.Property(x => x.EntityId).HasColumnName("entity_id");
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(50);
        b.Property(x => x.OldValueJson).HasColumnName("old_value").HasColumnType("jsonb");
        b.Property(x => x.NewValueJson).HasColumnName("new_value").HasColumnType("jsonb");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}