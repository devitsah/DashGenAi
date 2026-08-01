using AI_Dashboard.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI_Dashboard.Infrastructure.Postgres.Configurations;

public class PromptConfiguration : IEntityTypeConfiguration<Prompt>
{
    public void Configure(EntityTypeBuilder<Prompt> b)
    {
        b.ToTable("prompts");
        b.HasKey(x => new { x.TenantId, x.Id });

        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        b.Property(x => x.PromptText).HasColumnName("prompt_text");

  b.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);

        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.DashboardId).HasColumnName("dashboard_id");
        b.Property(x => x.ParentPromptId).HasColumnName("parent_prompt_id");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // Wire to Dashboard.Prompts navigation — must match the collection declared on Dashboard
        // to prevent EF from registering a second shadow relationship for the same FK columns.
        // SetNull (not Restrict): every dashboard created via the AI pipeline always has at least
        // one Prompt pointing back to it, so Restrict would make DELETE /api/dashboards/{id}
        // permanently impossible - it would always hit a FK violation.
        b.HasOne<Dashboard>()
            .WithMany(d => d.Prompts)
            .HasForeignKey(x => new { x.TenantId, x.DashboardId })
            .HasPrincipalKey(d => new { d.TenantId, d.Id })
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Self-referencing FK for clarification chains (ParentPromptId -> Prompt.Id).
        // Same reasoning as above for explicit configuration, but kept as Restrict here:
        // deleting a prompt that has clarification answers chained to it should be blocked,
        // since silently orphaning a clarification chain would corrupt prompt history.
        b.HasOne<Prompt>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ParentPromptId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}