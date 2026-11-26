using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Reports;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Infrastructure.Reports;

/// <summary>
/// Report SQL over the <c>report_orders</c> view. Raw SQL is NOT covered by the EF tenant filter:
/// every query filters by company (and optionally location) explicitly.
/// </summary>
public sealed class ReportQueries(AppDbContext db) : IReportQueries
{
    public async Task<ReportSummaryDto> SummaryAsync(ReportQuery query, CancellationToken ct)
    {
        var row = await db.Database.SqlQuery<SummaryRow>($"""
            SELECT
                count(*) FILTER (WHERE status IN ('Delivered', 'PickedUp'))::int AS "Completed",
                count(*) FILTER (WHERE status = 'Cancelled')::int AS "Cancelled",
                coalesce(sum(total) FILTER (WHERE status IN ('Delivered', 'PickedUp')), 0) AS "Revenue"
            FROM report_orders
            WHERE company_id = {query.CompanyId}
              AND (CAST({query.LocationId} AS uuid) IS NULL OR location_id = {query.LocationId})
              AND business_day BETWEEN {query.From} AND {query.To}
            """).SingleAsync(ct).ConfigureAwait(false);

        var average = row.Completed == 0 ? 0m : decimal.Round(row.Revenue / row.Completed, 2, MidpointRounding.AwayFromZero);
        return new ReportSummaryDto(row.Completed, row.Cancelled, row.Revenue, average);
    }

    public async Task<IReadOnlyList<RevenueRowDto>> RevenueAsync(ReportQuery query, CancellationToken ct) =>
        await db.Database.SqlQuery<RevenueRowDto>($"""
            SELECT
                business_day AS "Day",
                payment_method AS "PaymentMethod",
                count(*)::int AS "Orders",
                sum(total) AS "Total",
                sum(delivery_fee) AS "DeliveryFees"
            FROM report_orders
            WHERE company_id = {query.CompanyId}
              AND (CAST({query.LocationId} AS uuid) IS NULL OR location_id = {query.LocationId})
              AND business_day BETWEEN {query.From} AND {query.To}
              AND status IN ('Delivered', 'PickedUp')
            GROUP BY business_day, payment_method
            ORDER BY business_day, payment_method
            """).ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<BestSellerDto>> BestSellersAsync(ReportQuery query, int limit, CancellationToken ct) =>
        await db.Database.SqlQuery<BestSellerDto>($"""
            SELECT
                i.product_name || coalesce(' / ' || i.second_half_product_name, '') AS "ProductName",
                sum(i.quantity)::int AS "Quantity",
                sum(i.line_total) AS "Revenue"
            FROM order_items i
            JOIN report_orders o ON o.id = i.order_id
            WHERE o.company_id = {query.CompanyId}
              AND (CAST({query.LocationId} AS uuid) IS NULL OR o.location_id = {query.LocationId})
              AND o.business_day BETWEEN {query.From} AND {query.To}
              AND o.status IN ('Delivered', 'PickedUp')
            GROUP BY 1
            ORDER BY "Quantity" DESC, "Revenue" DESC
            LIMIT {limit}
            """).ToListAsync(ct).ConfigureAwait(false);

    public async Task<OrderTimesDto> TimesAsync(ReportQuery query, CancellationToken ct) =>
        await db.Database.SqlQuery<OrderTimesDto>($"""
            SELECT
                (avg(extract(epoch FROM accepted_at - placed_at)) / 60)::double precision AS "ToAccept",
                (avg(extract(epoch FROM ready_at - accepted_at)) / 60)::double precision AS "AcceptToReady",
                (avg(extract(epoch FROM completed_at - out_for_delivery_at)) / 60)::double precision AS "Delivery",
                (avg(extract(epoch FROM completed_at - placed_at)) / 60)::double precision AS "PlacedToCompleted"
            FROM report_orders
            WHERE company_id = {query.CompanyId}
              AND (CAST({query.LocationId} AS uuid) IS NULL OR location_id = {query.LocationId})
              AND business_day BETWEEN {query.From} AND {query.To}
              AND status IN ('Delivered', 'PickedUp')
            """).SingleAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<DriverResultDto>> DriversAsync(ReportQuery query, CancellationToken ct) =>
        await db.Database.SqlQuery<DriverResultDto>($"""
            SELECT
                driver_id AS "DriverId",
                NULL::text AS "DriverName",
                count(*)::int AS "Deliveries",
                (avg(extract(epoch FROM completed_at - out_for_delivery_at)) / 60)::double precision AS "AverageDeliveryMinutes",
                sum(total) AS "Total"
            FROM report_orders
            WHERE company_id = {query.CompanyId}
              AND (CAST({query.LocationId} AS uuid) IS NULL OR location_id = {query.LocationId})
              AND business_day BETWEEN {query.From} AND {query.To}
              AND status = 'Delivered'
              AND driver_id IS NOT NULL
            GROUP BY driver_id
            ORDER BY "Deliveries" DESC
            """).ToListAsync(ct).ConfigureAwait(false);

    private sealed record SummaryRow(int Completed, int Cancelled, decimal Revenue);
}
