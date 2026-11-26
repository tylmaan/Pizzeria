using Pizzeria.Domain.Common;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Domain.Customers;

public static class CustomerErrors
{
    public const string TooManyAddresses = "customer.too_many_addresses";
}

/// <summary>A delivery address saved in a customer's account, to pick at checkout.</summary>
public sealed class SavedAddress : ITenantEntity
{
    public const int LabelMaxLength = 50;
    public const int NotesMaxLength = 200;

    /// <summary>Enough for home, work and a few more; keeps the checkout picker short.</summary>
    public const int MaxPerCustomer = 10;

    private SavedAddress()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid CustomerId { get; private set; }

    public string? Label { get; private set; }

    public Address Address { get; private set; } = null!;

    /// <summary>For the driver, e.g. "domofon 12, drugie piętro".</summary>
    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <param name="existingCount">How many addresses the customer already has.</param>
    public static SavedAddress Create(Guid companyId, Guid customerId, string? label, Address address, string? notes, int existingCount, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (existingCount >= MaxPerCustomer)
        {
            throw new DomainException(CustomerErrors.TooManyAddresses);
        }

        var saved = new SavedAddress
        {
            Id = Guid.CreateVersion7(now),
            CompanyId = companyId,
            CustomerId = customerId,
            CreatedAt = now,
        };
        saved.Update(label, address, notes);
        return saved;
    }

    public void Update(string? label, Address address, string? notes)
    {
        ArgumentNullException.ThrowIfNull(address);
        Label = Trim(label, LabelMaxLength);
        Address = address;
        Notes = Trim(notes, NotesMaxLength);
    }

    private static string? Trim(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];
}
