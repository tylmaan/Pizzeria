namespace Pizzeria.Domain.Common;

/// <summary>Entity owned by a company (tenant). Subject to the global tenant query filter.</summary>
public interface ITenantEntity
{
    Guid CompanyId { get; }
}

/// <summary>Operational entity owned by a single location of a company.</summary>
public interface ILocationEntity : ITenantEntity
{
    Guid LocationId { get; }
}
