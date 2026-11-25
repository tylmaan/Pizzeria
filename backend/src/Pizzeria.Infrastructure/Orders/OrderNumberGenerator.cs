using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Orders;
using Pizzeria.Infrastructure.Persistence;

namespace Pizzeria.Infrastructure.Orders;

/// <summary>Row of the per-location, per-day order number counter.</summary>
public sealed class OrderCounter
{
    public Guid LocationId { get; set; }

    public DateOnly BusinessDay { get; set; }

    public int LastNumber { get; set; }
}

/// <summary>
/// Atomic "next number" with a single upsert: concurrent orders in one location never get the same number
/// (the row lock of ON CONFLICT DO UPDATE serialises them).
/// </summary>
public sealed class OrderNumberGenerator(AppDbContext db) : IOrderNumberGenerator
{
    public async Task<int> NextAsync(Guid locationId, DateOnly businessDay, CancellationToken ct)
    {
        var numbers = await db.Database.SqlQuery<int>($"""
            INSERT INTO order_counters (location_id, business_day, last_number)
            VALUES ({locationId}, {businessDay}, 1)
            ON CONFLICT (location_id, business_day)
            DO UPDATE SET last_number = order_counters.last_number + 1
            RETURNING last_number AS "Value"
            """).ToListAsync(ct).ConfigureAwait(false);
        return numbers.Single();
    }
}
