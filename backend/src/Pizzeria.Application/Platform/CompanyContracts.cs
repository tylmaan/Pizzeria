using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Platform;

public sealed record CompanyDto(Guid Id, string Name, string Slug, DateTimeOffset CreatedAt)
{
    public static CompanyDto From(Company company) => new(company.Id, company.Name, company.Slug, company.CreatedAt);
}

/// <summary>Onboards a new company together with its first company administrator.</summary>
public sealed record CreateCompanyRequest(
    string Name,
    string Slug,
    string AdminEmail,
    string AdminPassword,
    string AdminDisplayName);
