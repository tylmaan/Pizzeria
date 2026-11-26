using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Application.Reports;

/// <summary>
/// Sales reports. Company admins see any location or the whole company; location managers only their own
/// locations, one at a time. A "day" is the local business day of the location.
/// </summary>
public sealed class ReportService(ITenantContext tenant, IReportQueries queries, IUserAccountService accounts)
{
    public const int MaxDays = 366;
    public const int BestSellersLimit = 20;

    public async Task<ReportDto> GetAsync(Guid? locationId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var companyId = Authorize(locationId);
        if (from > to || to.DayNumber - from.DayNumber >= MaxDays)
        {
            throw new UnprocessableException(ErrorCodes.InvalidReportRange);
        }

        var query = new ReportQuery(companyId, locationId, from, to);
        var summary = await queries.SummaryAsync(query, ct).ConfigureAwait(false);
        var revenue = await queries.RevenueAsync(query, ct).ConfigureAwait(false);
        var bestSellers = await queries.BestSellersAsync(query, BestSellersLimit, ct).ConfigureAwait(false);
        var times = await queries.TimesAsync(query, ct).ConfigureAwait(false);
        var drivers = await queries.DriversAsync(query, ct).ConfigureAwait(false);

        var names = (await accounts.ListAsync(companyId, null, ct).ConfigureAwait(false)).ToDictionary(u => u.Id, u => u.DisplayName);
        return new ReportDto(
            locationId,
            from,
            to,
            summary,
            revenue,
            bestSellers,
            times,
            [.. drivers.Select(d => d with { DriverName = names.GetValueOrDefault(d.DriverId) })]);
    }

    private Guid Authorize(Guid? locationId)
    {
        if (tenant.CompanyId is not { } companyId)
        {
            throw new ForbiddenException();
        }

        if (tenant.IsCompanyAdmin)
        {
            return companyId;
        }

        // Managers: one of their own locations, never the company-wide figures.
        if (tenant.IsInRole(Roles.LocationManager) && locationId is { } id && tenant.CanAccessLocation(id))
        {
            return companyId;
        }

        throw new ForbiddenException();
    }
}
