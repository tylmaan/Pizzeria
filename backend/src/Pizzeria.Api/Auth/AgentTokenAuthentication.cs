using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Pizzeria.Application.Printing;
using Pizzeria.Application.Tenancy;
using Pizzeria.Domain.Identity;

namespace Pizzeria.Api.Auth;

/// <summary>
/// Device token of the local print agent (<c>Authorization: Bearer {token}</c>, or <c>access_token</c> in the query).
/// Only the agent hub uses this scheme, so an agent token opens nothing else.
/// </summary>
public sealed class AgentTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AgentAuthenticationService agents)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken(Request);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var agent = await agents.FindActiveAsync(token, Context.RequestAborted).ConfigureAwait(false);
        if (agent is null)
        {
            return AuthenticateResult.Fail("Unknown or revoked agent token.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, agent.AgentId.ToString()),
                new Claim(ClaimTypes.Name, agent.Name),
                new Claim(ClaimTypes.Role, Roles.Agent),
                new Claim(TenantClaims.CompanyId, agent.CompanyId.ToString()),
                new Claim(TenantClaims.LocationId, agent.LocationId.ToString()),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    private static string? ReadToken(HttpRequest request)
    {
        const string prefix = "Bearer ";
        var header = request.Headers.Authorization.ToString();
        if (header.StartsWith(prefix, true, CultureInfo.InvariantCulture))
        {
            return header[prefix.Length..].Trim();
        }

        return request.Query["access_token"] is { Count: > 0 } query ? query.ToString() : null;
    }
}
