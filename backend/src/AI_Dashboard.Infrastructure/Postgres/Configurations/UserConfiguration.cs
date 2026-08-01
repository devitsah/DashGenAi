using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI_Dashboard.Infrastructure.Postgres.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();

        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}