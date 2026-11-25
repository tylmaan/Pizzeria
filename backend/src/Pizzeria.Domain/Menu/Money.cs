using Pizzeria.Domain.Common;

namespace Pizzeria.Domain.Menu;

/// <summary>Rules for amounts in PLN stored as <c>numeric(10,2)</c>.</summary>
public static class Money
{
    public const decimal MaxPrice = 99_999_999.99m;

    /// <summary>A menu price: non-negative, at most two decimal places, fits <c>numeric(10,2)</c>.</summary>
    public static decimal RequirePrice(decimal price)
    {
        if (price < 0 || price > MaxPrice || decimal.Round(price, 2) != price)
        {
            throw new DomainException(MenuErrors.InvalidPrice);
        }

        return price;
    }

    /// <summary>Rounds to grosze, halves away from zero (the usual commercial rounding).</summary>
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
