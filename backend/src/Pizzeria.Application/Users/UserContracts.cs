namespace Pizzeria.Application.Users;

public sealed record UserDto(Guid Id, string Email, string DisplayName, string Role, IReadOnlyList<Guid> LocationIds);

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role,
    IReadOnlyList<Guid>? LocationIds);

/// <summary>Account to create in the identity store. Already authorized and validated.</summary>
public sealed record NewUserAccount(
    Guid? CompanyId,
    string Email,
    string Password,
    string DisplayName,
    string Role,
    IReadOnlyList<Guid> LocationIds);

/// <summary>Identity store operations (implemented with ASP.NET Core Identity in Infrastructure).</summary>
public interface IUserAccountService
{
    /// <exception cref="Errors.ConflictException">Email already in use.</exception>
    /// <exception cref="FluentValidation.ValidationException">Password does not meet the policy.</exception>
    Task<Guid> CreateAsync(NewUserAccount account, CancellationToken ct);

    /// <param name="companyId">Company whose users to list.</param>
    /// <param name="locationIds">When set, only users assigned to at least one of these locations.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<UserDto>> ListAsync(Guid companyId, IReadOnlyCollection<Guid>? locationIds, CancellationToken ct);
}
