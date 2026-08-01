using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Common;
using AI_Dashboard.Domain.Entities;
using AI_Dashboard.Infrastructure.Postgres.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AI_Dashboard.Infrastructure.Postgres;

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
);

public class AppDbContext : DbContext
{
    // Optional: design-time tooling / tests that construct AppDbContext with just
    // options (no HTTP context available) still work, since this defaults to null
    // and the audit stamping below simply skips CreatedBy/UpdatedBy in that case.
    private readonly ICurrentUserService? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<Widget> Widgets => Set<Widget>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<Query> Queries => Set<Query>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
// OVERRIDE ISLIE KIA NHI TOH BAR BAR AUDIT LOGS MIE LIKHNA PDTA
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new DashboardConfiguration());
        modelBuilder.ApplyConfiguration(new WidgetConfiguration());
        modelBuilder.ApplyConfiguration(new PromptConfiguration());
        modelBuilder.ApplyConfiguration(new QueryConfiguration());
        modelBuilder.ApplyConfiguration(new UserSessionConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    // Centralizes what used to be scattered `entity.UpdatedAt = DateTime.UtcNow;`
    // lines in individual repositories: every insert gets CreatedAt/CreatedBy,
    // every update gets UpdatedAt/UpdatedBy, for any entity that opts in via
    // ITimestamped / IAuditableEntity - no repository needs to remember to do this.
    private void StampAuditFields()
    {
        var utcNow = DateTime.UtcNow;
        var userId = _currentUser is { IsAuthenticated: true } ? _currentUser.UserId : (int?)null;

        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    if (entry.Entity is IAuditableEntity created && userId.HasValue)
                        created.CreatedBy = userId.Value;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    if (entry.Entity is IAuditableEntity updated && userId.HasValue)
                        updated.UpdatedBy = userId.Value;
                    break;
            }
        }
    }
}