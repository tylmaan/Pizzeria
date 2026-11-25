namespace Pizzeria.Application.Tenancy;

/// <summary>Resolves the tenant of an anonymous request (customer shop) from the company slug.</summary>
public interface ICompanyResolver
{
    Task<Guid?> FindCompanyIdBySlugAsync(string slug, CancellationToken ct);
}
