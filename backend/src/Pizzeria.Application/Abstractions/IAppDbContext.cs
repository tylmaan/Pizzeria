using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Abstractions;

/// <summary>
/// Persistence used by use cases. Tenant entities are filtered to the current company by a global
/// query filter; location-level access still has to be checked via <see cref="Tenancy.ITenantContext"/>.
/// </summary>
public interface IAppDbContext
{
    DbSet<Company> Companies { get; }

    DbSet<Location> Locations { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
