using System.Text.RegularExpressions;
using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Tenancy;

/// <summary>A restaurant chain — the tenant. Owns the menu, prices, locations and staff.</summary>
public sealed partial class Company
{
    public const int NameMaxLength = 200;
    public const int SlugMaxLength = 63;

    private Company()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Subdomain of the customer shop, e.g. <c>dominos</c> → <c>dominos.platform.pl</c>.</summary>
    public string Slug { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Company Create(string name, string slug, DateTimeOffset now)
    {
        var company = new Company
        {
            Id = Guid.CreateVersion7(now),
            Slug = NormalizeSlug(slug),
            CreatedAt = now,
        };
        company.Rename(name);
        return company;
    }

    public void Rename(string name)
    {
        Name = RequireName(name);
    }

    private static string RequireName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > NameMaxLength)
        {
            throw new DomainException(TenancyErrors.InvalidName);
        }

        return trimmed;
    }

    private static string NormalizeSlug(string slug)
    {
        var normalized = slug?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < 2 or > SlugMaxLength || !SlugPattern().IsMatch(normalized))
        {
            throw new DomainException(TenancyErrors.InvalidSlug);
        }

        return normalized;
    }

    // DNS label: lowercase letters, digits and single hyphens, not at the start or end.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
