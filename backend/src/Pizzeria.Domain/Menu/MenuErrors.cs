namespace Pizzeria.Domain.Menu;

public static class MenuErrors
{
    public const string InvalidName = "menu.invalid_name";
    public const string InvalidPrice = "menu.invalid_price";
    public const string SizesRequired = "menu.sizes_required";
    public const string TooManySizes = "menu.too_many_sizes";
    public const string DuplicateSizeName = "menu.duplicate_size_name";
    public const string UnknownSize = "menu.unknown_size";
    public const string PricesRequired = "menu.prices_required";
    public const string CategoryMismatch = "menu.category_mismatch";
    public const string InvalidSelectionLimits = "menu.invalid_selection_limits";
    public const string UnknownModifier = "menu.unknown_modifier";

    // Pricing an order item.
    public const string ProductUnavailable = "menu.product_unavailable";
    public const string SizeUnavailable = "menu.size_unavailable";
    public const string HalvesNotAllowed = "menu.halves_not_allowed";
    public const string ModifierUnavailable = "menu.modifier_unavailable";
    public const string ModifierSelectionInvalid = "menu.modifier_selection_invalid";
}
