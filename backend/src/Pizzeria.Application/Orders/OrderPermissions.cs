using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Orders;

namespace Pizzeria.Application.Orders;

/// <summary>
/// Which roles may move an order to which status. Location access is checked separately;
/// a driver may only touch orders assigned to them.
/// </summary>
public static class OrderPermissions
{
    private static readonly string[] FrontOfHouse = [Roles.CompanyAdmin, Roles.LocationManager, Roles.Staff];

    private static readonly Dictionary<OrderStatus, string[]> AllowedRoles = new()
    {
        [OrderStatus.Accepted] = FrontOfHouse,
        [OrderStatus.InPreparation] = [.. FrontOfHouse, Roles.Kitchen],
        [OrderStatus.Ready] = [.. FrontOfHouse, Roles.Kitchen],
        [OrderStatus.PickedUp] = FrontOfHouse,
        [OrderStatus.AssignedToDriver] = FrontOfHouse,
        [OrderStatus.InDelivery] = [.. FrontOfHouse, Roles.Driver],
        [OrderStatus.Delivered] = [.. FrontOfHouse, Roles.Driver],
        [OrderStatus.Cancelled] = FrontOfHouse,
    };

    public static bool CanMoveTo(ITenantContext user, Order order, OrderStatus target)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(order);

        if (!AllowedRoles.TryGetValue(target, out var roles))
        {
            return false;
        }

        if (roles.Where(r => r != Roles.Driver).Any(user.IsInRole))
        {
            return true;
        }

        // Drivers act only on their own deliveries.
        return roles.Contains(Roles.Driver) && user.IsInRole(Roles.Driver) && order.DriverId == user.UserId;
    }

    /// <summary>Whether the user may see the order at all (drivers: only their own).</summary>
    public static bool CanView(ITenantContext user, Order order)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(order);

        if (!user.CanAccessLocation(order.LocationId))
        {
            return false;
        }

        var onlyDriver = user.IsInRole(Roles.Driver) && !FrontOfHouse.Any(user.IsInRole) && !user.IsInRole(Roles.Kitchen);
        return !onlyDriver || order.DriverId == user.UserId;
    }
}
