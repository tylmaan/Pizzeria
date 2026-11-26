namespace Pizzeria.Application.Errors;

/// <summary>Error codes returned in ProblemDetails (<c>code</c>). The frontend translates them via i18n.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "common.validation_failed";
    public const string NotFound = "common.not_found";
    public const string Forbidden = "common.forbidden";
    public const string Unauthorized = "auth.unauthorized";
    public const string Unexpected = "common.unexpected";
    public const string TooManyRequests = "common.too_many_requests";

    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string LockedOut = "auth.locked_out";

    public const string CompanySlugTaken = "company.slug_taken";
    public const string LocationNotFound = "location.not_found";

    public const string CategoryNotFound = "menu.category_not_found";
    public const string ProductNotFound = "menu.product_not_found";
    public const string ModifierGroupNotFound = "menu.modifier_group_not_found";
    public const string SizeInUse = "menu.size_in_use";

    public const string DeliveryZoneNotFound = "delivery.zone_not_found";
    public const string AddressNotFound = "delivery.address_not_found";
    public const string ShopNotFound = "delivery.shop_not_found";
    public const string AddressNotDelivered = "delivery.address_not_delivered";

    public const string OrderNotFound = "order.not_found";
    public const string NoNextStatus = "order.no_next_status";
    public const string StatusChangeNotAllowed = "order.status_change_not_allowed";
    public const string DriverNotFound = "order.driver_not_found";

    public const string UserEmailTaken = "user.email_taken";
    public const string RoleNotAssignable = "user.role_not_assignable";
    public const string LocationsRequired = "user.locations_required";
    public const string LocationsNotAllowed = "user.locations_not_allowed";
}
