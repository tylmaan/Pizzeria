using System.Security.Claims;
using Microsoft.Extensions.Options;
using Pizzeria.Application.Tenancy;

namespace Pizzeria.Api.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>
    /// Root domain of customer shops. <c>dominos.{ShopRootDomain}</c> resolves to the company with slug <c>dominos</c>.
    /// In development <c>localhost</c> works out of the box (<c>demo.localhost</c>).
    /// </summary>
    public string ShopRootDomain { get; set; } = "localhost";
}

/// <summary>
/// Fills <see cref="TenantContext"/>: from the access token for signed-in users, otherwise from the shop host name.
/// Used per HTTP request (middleware) and per SignalR hub invocation (hub filter).
/// </summary>
public sealed class TenantResolver(TenantContext tenant, ICompanyResolver companies, IOptions<TenancyOptions> options)
{
    public async Task ResolveAsync(ClaimsPrincipal user, string host, CancellationToken ct)
    {
        if (user.Identity?.IsAuthenticated == true)
        {
            tenant.SetUser(
                Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
                ParseGuid(user.FindFirstValue(TenantClaims.CompanyId)),
                user.FindAll(ClaimTypes.Role).Select(c => c.Value),
                user.FindAll(TenantClaims.LocationId).Select(c => Guid.Parse(c.Value)));
        }
        else if (FindShopSlug(host, options.Value.ShopRootDomain) is { } slug
                 && await companies.FindCompanyIdBySlugAsync(slug, ct).ConfigureAwait(false) is { } companyId)
        {
            tenant.SetAnonymous(companyId);
        }
    }

    public static string? FindShopSlug(string host, string rootDomain)
    {
        ArgumentNullException.ThrowIfNull(host);

        var suffix = "." + rootDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var slug = host[..^suffix.Length].ToLowerInvariant();
        return slug.Length > 0 && !slug.Contains('.', StringComparison.Ordinal) ? slug : null;
    }

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;
}

/// <summary>Resolves the tenant for every HTTP request. Must run after authentication.</summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantResolver resolver)
    {
        await resolver.ResolveAsync(context.User, context.Request.Host.Host, context.RequestAborted).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}
