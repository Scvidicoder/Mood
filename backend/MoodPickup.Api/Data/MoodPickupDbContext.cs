using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data;

public sealed class MoodPickupDbContext(DbContextOptions<MoodPickupDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<LoginChallenge> LoginChallenges => Set<LoginChallenge>();

    public DbSet<TelegramProcessedUpdate> TelegramProcessedUpdates =>
        Set<TelegramProcessedUpdate>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<EmployeeRole> EmployeeRoles => Set<EmployeeRole>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<OptionGroup> OptionGroups => Set<OptionGroup>();

    public DbSet<OptionValue> OptionValues => Set<OptionValue>();

    public DbSet<ProductOptionGroup> ProductOptionGroups => Set<ProductOptionGroup>();

    public DbSet<ProductOptionValue> ProductOptionValues => Set<ProductOptionValue>();

    public DbSet<EmployeeActionLog> EmployeeActionLogs => Set<EmployeeActionLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareMenuEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareMenuEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoodPickupDbContext).Assembly);
    }

    private void PrepareMenuEntities()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IHasNormalizedName>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.Name = entry.Entity.Name.Trim();
            entry.Entity.NormalizedName = entry.Entity.Name.ToLowerInvariant();
        }

        foreach (var entry in ChangeTracker.Entries<IHasCreatedAt>()
                     .Where(entry => entry.State == EntityState.Added))
        {
            entry.Entity.CreatedAt = now;
        }

        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IHasCreatedAt.CreatedAt)).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<IHasConcurrencyToken>())
        {
            if (entry.State == EntityState.Added && entry.Entity.RowVersion == Guid.Empty)
            {
                entry.Entity.RowVersion = Guid.NewGuid();
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid();
            }
        }
    }
}
