using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Pizzeria.Application.Errors;

namespace Pizzeria.Api.Errors;

public static class ProblemDetailsCodes
{
    public const string CodeKey = "code";

    /// <summary>Ensures every ProblemDetails response (also framework-generated 401/403/404/500) has a <c>code</c>.</summary>
    public static void AddDefaultCode(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problem = context.ProblemDetails;
        problem.Extensions.Remove("traceId");
        problem.Extensions["traceId"] = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity.Id
                                        ?? context.HttpContext.TraceIdentifier;

        if (problem.Extensions.ContainsKey(CodeKey))
        {
            return;
        }

        problem.Extensions[CodeKey] = problem.Status switch
        {
            StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
            StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
            StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
            StatusCodes.Status404NotFound => ErrorCodes.NotFound,
            StatusCodes.Status429TooManyRequests => ErrorCodes.TooManyRequests,
            _ => ErrorCodes.Unexpected,
        };
    }

    /// <summary>
    /// FluentValidation built-in codes (<c>NotEmptyValidator</c>) become <c>validation.not_empty</c>;
    /// custom codes that already contain a dot are kept as they are.
    /// </summary>
    public static string ForValidator(string errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);

        if (errorCode.Contains('.', StringComparison.Ordinal))
        {
            return errorCode;
        }

        var name = errorCode.EndsWith("Validator", StringComparison.Ordinal) ? errorCode[..^"Validator".Length] : errorCode;
        var builder = new StringBuilder("validation.");
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }
}
