namespace Pizzeria.Application.Errors;

/// <summary>Base for application errors mapped by the API to ProblemDetails with a machine-readable code.</summary>
public abstract class AppException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>Resource does not exist or the caller has no access to it (we do not reveal which).</summary>
public sealed class NotFoundException(string code = ErrorCodes.NotFound) : AppException(code);

public sealed class ForbiddenException(string code = ErrorCodes.Forbidden) : AppException(code);

public sealed class ConflictException(string code) : AppException(code);

/// <summary>The request is valid but cannot be processed, e.g. an address that cannot be located.</summary>
public sealed class UnprocessableException(string code) : AppException(code);
