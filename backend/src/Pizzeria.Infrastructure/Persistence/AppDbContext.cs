using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Common;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Drivers;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Payments;
using Pizzeria.Domain.Printing;
using Pizzeria.Domain.Tenancy;
using Pizzeria.Infrastructure.Identity;

namespace Pizzeria.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    /// <summary>Name of the global tenant query filter (for targeted <c>IgnoreQueryFilters([...])</c>).</summary>
    public const string TenantFilter = "Tenant";

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();

    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<DriverShift> DriverShifts => Set<DriverShift>();

    public DbSet<DriverPosition> DriverPositions => Set<DriverPosition>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PrintAgent> PrintAgents => Set<PrintAgent>();

    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    public DbSet<UserLocation> UserLocations => Set<UserLocation>();

    // Evaluated per query: EF parameterizes members of the context instance used in query filters.
    private Guid? CurrentCompanyId => tenant.CompanyId;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantOnWrite();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantOnWrite();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money: always numeric(10,2).
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("postgis");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<Company>().HasQueryFilter(TenantFilter, c => c.Id == CurrentCompanyId);

        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType) && !t.IsOwned()))
        {
            ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }

        SnakeCaseNaming.Apply(builder);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(TenantFilter, e => e.CompanyId == CurrentCompanyId);
    }

    /// <summary>
    /// Defense in depth: the query filter protects reads, this protects writes — a tenant entity can
    /// only be created in, or changed within, the current company.
    /// </summary>
    private void EnforceTenantOnWrite()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            var companyId = entry.Property(e => e.CompanyId);
            var isForeign = entry.State switch
            {
                EntityState.Added => entry.Entity.CompanyId != tenant.CompanyId,
                EntityState.Modified or EntityState.Deleted =>
                    companyId.IsModified || companyId.OriginalValue != tenant.CompanyId,
                _ => false,
            };

            if (isForeign)
            {
                throw new InvalidOperationException(
                    $"Cross-tenant write blocked for {entry.Metadata.ClrType.Name} ({entry.State}).");
            }
        }
    }
}
