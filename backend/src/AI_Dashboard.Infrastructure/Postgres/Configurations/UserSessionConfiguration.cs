using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI_Dashboard.Infrastructure.Postgres.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
      b.ToTable("user_sessions");
        b.HasKey(x => new { x.TenantId, x.Id });

        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(255);
     
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UserId })
            .HasPrincipalKey(u => new { u.TenantId, u.Id });
    }
}