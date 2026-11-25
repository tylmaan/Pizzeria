using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pizzeria.Application.Tenancy;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Infrastructure.Tenancy;

public sealed class CompanyResolver(AppDbContext db, IMemoryCache cache) : ICompanyResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<Guid?> FindCompanyIdBySlugAsync(string slug, CancellationToken ct)
    {
        var key = $"company-slug:{slug}";
        if (cache.TryGetValue(key, out Guid? cached))
        {
            return cached;
        }

        // This lookup establishes the tenant itself, so it cannot run under the tenant filter.
        var companyId = await db.Companies
            .IgnoreQueryFilters()
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        cache.Set(key, companyId, CacheDuration);
        return companyId;
    }
}
