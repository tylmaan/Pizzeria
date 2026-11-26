using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Customers;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Customers;

/// <summary>The customer's own account in a shop: profile, saved addresses, order history, deletion.</summary>
public sealed class CustomerAccountService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock,
    ICustomerIdentityService identity,
    IValidator<RegisterCustomerRequest> registerValidator,
    IValidator<UpdateCustomerProfileRequest> profileValidator,
    IValidator<SaveAddressRequest> addressValidator)
{
    public const int HistoryLimit = 50;

    /// <summary>Creates an account in the shop's company (resolved from the shop address, anonymous request).</summary>
    public async Task<Guid> RegisterAsync(RegisterCustomerRequest request, CancellationToken ct)
    {
        if (tenant.CompanyId is not { } companyId || tenant.UserId is not null)
        {
            throw new NotFoundException(ErrorCodes.ShopNotFound);
        }

        await registerValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        return await identity.CreateAsync(companyId, request.Email.Trim(), request.Password, request.Name.Trim(), Normalize(request.Phone), ct)
            .ConfigureAwait(false);
    }

    public async Task<CustomerProfileDto> GetProfileAsync(CancellationToken ct)
    {
        var (companyId, customerId) = RequireCustomer();
        return await identity.FindAsync(companyId, customerId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.NotFound);
    }

    public async Task<CustomerProfileDto> UpdateProfileAsync(UpdateCustomerProfileRequest request, CancellationToken ct)
    {
        var (companyId, customerId) = RequireCustomer();
        await profileValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        await identity.UpdateAsync(companyId, customerId, request.Name.Trim(), Normalize(request.Phone), ct).ConfigureAwait(false);
        return await GetProfileAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SavedAddressDto>> ListAddressesAsync(CancellationToken ct)
    {
        var (_, customerId) = RequireCustomer();
        var addresses = await db.SavedAddresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
        return addresses.ConvertAll(ToDto);
    }

    public async Task<SavedAddressDto> AddAddressAsync(SaveAddressRequest request, CancellationToken ct)
    {
        var (companyId, customerId) = RequireCustomer();
        await addressValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        var count = await db.SavedAddresses.CountAsync(a => a.CustomerId == customerId, ct).ConfigureAwait(false);

        var address = SavedAddress.Create(companyId, customerId, request.Label, ToAddress(request), request.Notes, count, clock.GetUtcNow());
        db.SavedAddresses.Add(address);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToDto(address);
    }

    public async Task<SavedAddressDto> UpdateAddressAsync(Guid id, SaveAddressRequest request, CancellationToken ct)
    {
        var address = await FindOwnAddressAsync(id, ct).ConfigureAwait(false);
        await addressValidator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);
        address.Update(request.Label, ToAddress(request), request.Notes);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToDto(address);
    }

    public async Task DeleteAddressAsync(Guid id, CancellationToken ct)
    {
        var address = await FindOwnAddressAsync(id, ct).ConfigureAwait(false);
        db.SavedAddresses.Remove(address);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CustomerOrderDto>> ListOrdersAsync(CancellationToken ct)
    {
        var (_, customerId) = RequireCustomer();
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAt)
            .Take(HistoryLimit)
            .ToListAsync(ct).ConfigureAwait(false);
        return orders.ConvertAll(o => new CustomerOrderDto(
            o.Id,
            o.Number,
            o.PlacedAt,
            o.Status,
            o.Fulfillment,
            o.Total,
            o.TrackingToken,
            [.. o.Items.OrderBy(i => i.SortOrder).Select(i => $"{i.Quantity}× {i.ProductName}{(i.SecondHalfProductName is null ? string.Empty : " / " + i.SecondHalfProductName)} {i.SizeName}")]));
    }

    /// <summary>
    /// Deletes the account and its saved addresses. Orders stay (the restaurant needs them for accounting)
    /// but lose the link to the account.
    /// </summary>
    public async Task DeleteAccountAsync(CancellationToken ct)
    {
        var (companyId, customerId) = RequireCustomer();
        await db.SavedAddresses.Where(a => a.CustomerId == customerId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await identity.DeleteAsync(companyId, customerId, ct).ConfigureAwait(false);
    }

    private async Task<SavedAddress> FindOwnAddressAsync(Guid id, CancellationToken ct)
    {
        var (_, customerId) = RequireCustomer();
        return await db.SavedAddresses.FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(ErrorCodes.NotFound);
    }

    private (Guid CompanyId, Guid CustomerId) RequireCustomer() =>
        tenant.IsInRole(Roles.Customer) && tenant.CompanyId is { } companyId && tenant.UserId is { } customerId
            ? (companyId, customerId)
            : throw new ForbiddenException();

    private static Address ToAddress(SaveAddressRequest r) => new(r.Street.Trim(), r.PostalCode.Trim(), r.City.Trim());

    private static SavedAddressDto ToDto(SavedAddress a) =>
        new(a.Id, a.Label, a.Address.Street, a.Address.PostalCode, a.Address.City, a.Notes);

    private static string? Normalize(string? phone) => string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}
