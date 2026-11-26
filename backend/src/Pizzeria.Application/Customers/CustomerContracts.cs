using Pizzeria.Domain.Orders;

namespace Pizzeria.Application.Customers;

public sealed record RegisterCustomerRequest(string Email, string Password, string Name, string? Phone);

public sealed record CustomerLoginRequest(string Email, string Password);

public sealed record CustomerProfileDto(string Email, string Name, string? Phone);

public sealed record UpdateCustomerProfileRequest(string Name, string? Phone);

public sealed record SavedAddressDto(Guid Id, string? Label, string Street, string PostalCode, string City, string? Notes);

public sealed record SaveAddressRequest(string? Label, string Street, string PostalCode, string City, string? Notes);

/// <summary>An order in the customer's history; the tracking token opens its status page.</summary>
public sealed record CustomerOrderDto(
    Guid Id,
    int Number,
    DateTimeOffset PlacedAt,
    OrderStatus Status,
    FulfillmentType Fulfillment,
    decimal Total,
    string TrackingToken,
    IReadOnlyList<string> Items);

/// <summary>
/// Customer accounts are per company: the same e-mail can have separate accounts in two pizzerias, and a customer
/// of one company is unknown to another. Identity keeps user names globally unique, so a customer's user name
/// is prefixed with the company (staff log in with the bare e-mail).
/// </summary>
public static class CustomerUserNames
{
    public static string For(Guid companyId, string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return $"{companyId:N}+{email.Trim()}";
    }
}

/// <summary>Customer users in the identity store (users are not under the tenant filter: pass the company).</summary>
public interface ICustomerIdentityService
{
    /// <exception cref="Errors.ConflictException">An account with this e-mail already exists in the company.</exception>
    /// <exception cref="FluentValidation.ValidationException">Password does not meet the policy.</exception>
    Task<Guid> CreateAsync(Guid companyId, string email, string password, string name, string? phone, CancellationToken ct);

    Task<CustomerProfileDto?> FindAsync(Guid companyId, Guid customerId, CancellationToken ct);

    Task UpdateAsync(Guid companyId, Guid customerId, string name, string? phone, CancellationToken ct);

    Task DeleteAsync(Guid companyId, Guid customerId, CancellationToken ct);
}
