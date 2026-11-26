using System.Text;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Pizzeria.Application.Errors;

namespace Pizzeria.Infrastructure.Identity;

/// <summary>Turns Identity errors into the application's exceptions (409 for a taken e-mail, 400 for the password policy).</summary>
internal static class IdentityErrors
{
    public static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName)
                                         or nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            throw new ConflictException(ErrorCodes.UserEmailTaken);
        }

        var failures = result.Errors.Select(e => new ValidationFailure(
            e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : "Email",
            e.Description)
        {
            ErrorCode = "validation." + ToSnakeCase(e.Code),
        });
        throw new ValidationException(failures);
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(value[i]));
        }

        return builder.ToString();
    }
}
