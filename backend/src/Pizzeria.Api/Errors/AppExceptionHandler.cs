using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pizzeria.Application.Errors;
using Pizzeria.Domain.Common;

namespace Pizzeria.Api.Errors;

/// <summary>Maps application and domain exceptions to ProblemDetails carrying a machine-readable <c>code</c>.</summary>
public sealed class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, errors) = exception switch
        {
            ValidationException e => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, ToErrors(e)),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Code, null),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Code, null),
            ConflictException e => (StatusCodes.Status409Conflict, e.Code, null),
            UnprocessableException e => (StatusCodes.Status422UnprocessableEntity, e.Code, null),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Code, null),
            _ => (0, null, null),
        };

        if (code is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Extensions = { [ProblemDetailsCodes.CodeKey] = code } };
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        }).ConfigureAwait(false);
    }

    /// <summary>Field → list of error codes, e.g. <c>{"address.postalCode": ["validation.postal_code"]}</c>.</summary>
    private static Dictionary<string, string[]> ToErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(f => ToCamelCasePath(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => ProblemDetailsCodes.ForValidator(f.ErrorCode)).Distinct().ToArray());

    private static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
}
