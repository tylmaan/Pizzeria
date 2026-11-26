using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Drivers;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Payments;
using Pizzeria.Domain.Printing;
using Pizzeria.Domain.Tenancy;
using Location = Pizzeria.Domain.Tenancy.Location;

namespace Pizzeria.Application.Abstractions;

/// <summary>
/// Persistence used by use cases. Tenant entities are filtered to the current company by a global
/// query filter; location-level access still has to be checked via <see cref="Tenancy.ITenantContext"/>.
/// </summary>
public interface IAppDbContext
{
    DbSet<Company> Companies { get; }

    DbSet<Location> Locations { get; }

    DbSet<Category> Categories { get; }

    DbSet<Product> Products { get; }

    DbSet<ModifierGroup> ModifierGroups { get; }

    DbSet<DeliveryZone> DeliveryZones { get; }

    DbSet<Order> Orders { get; }

    DbSet<DriverShift> DriverShifts { get; }

    DbSet<DriverPosition> DriverPositions { get; }

    DbSet<Payment> Payments { get; }

    DbSet<PrintAgent> PrintAgents { get; }

    DbSet<PrintJob> PrintJobs { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
