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

    public const string UserEmailTaken = "user.email_taken";
    public const string RoleNotAssignable = "user.role_not_assignable";
    public const string LocationsRequired = "user.locations_required";
    public const string LocationsNotAllowed = "user.locations_not_allowed";
}
