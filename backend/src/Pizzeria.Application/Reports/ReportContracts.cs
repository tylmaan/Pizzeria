namespace Pizzeria.Application.Reports;

/// <param name="LocationId">One location, or <c>null</c> for the whole company.</param>
/// <param name="From">First business day (local day of the location), inclusive.</param>
/// <param name="To">Last business day, inclusive.</param>
public sealed record ReportQuery(Guid CompanyId, Guid? LocationId, DateOnly From, DateOnly To);

public sealed record ReportSummaryDto(int CompletedOrders, int CancelledOrders, decimal Revenue, decimal AverageOrderValue);

/// <summary>Completed orders of one day paid with one method.</summary>
public sealed record RevenueRowDto(DateOnly Day, string PaymentMethod, int Orders, decimal Total, decimal DeliveryFees);

public sealed record BestSellerDto(string ProductName, int Quantity, decimal Revenue);

/// <summary>Average minutes between stages of completed orders (<c>null</c> when there is no data).</summary>
public sealed record OrderTimesDto(double? ToAccept, double? AcceptToReady, double? Delivery, double? PlacedToCompleted);

public sealed record DriverResultDto(Guid DriverId, string? DriverName, int Deliveries, double? AverageDeliveryMinutes, decimal Total);

public sealed record ReportDto(
    Guid? LocationId,
    DateOnly From,
    DateOnly To,
    ReportSummaryDto Summary,
    IReadOnlyList<RevenueRowDto> Revenue,
    IReadOnlyList<BestSellerDto> BestSellers,
    OrderTimesDto Times,
    IReadOnlyList<DriverResultDto> Drivers);

/// <summary>Report figures computed by the database (Infrastructure: SQL over the <c>report_orders</c> view).</summary>
public interface IReportQueries
{
    Task<ReportSummaryDto> SummaryAsync(ReportQuery query, CancellationToken ct);

    Task<IReadOnlyList<RevenueRowDto>> RevenueAsync(ReportQuery query, CancellationToken ct);

    Task<IReadOnlyList<BestSellerDto>> BestSellersAsync(ReportQuery query, int limit, CancellationToken ct);

    Task<OrderTimesDto> TimesAsync(ReportQuery query, CancellationToken ct);

    /// <summary>Driver results without names (names come from the identity store).</summary>
    Task<IReadOnlyList<DriverResultDto>> DriversAsync(ReportQuery query, CancellationToken ct);
}
