namespace Pizzeria.Domain.Common;

/// <summary>
/// A business rule was violated. <see cref="Code"/> is a stable, machine-readable error code
/// (e.g. <c>company.invalid_slug</c>) that the frontend translates via i18n.
/// </summary>
public class DomainException(string code, string? message = null) : Exception(message ?? code)
{
    public string Code { get; } = code;
}
