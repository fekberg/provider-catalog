using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ProviderCatalog.Auth;

/// <summary>
/// Accepts "Authorization: Bearer &lt;token&gt;" when the token is one of the configured access tokens.
/// </summary>
public sealed class AccessTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AccessTokens tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AccessToken";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var owner = tokens.FindOwner(header["Bearer ".Length..].Trim());

        if (owner is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("The access token isn't valid."));
        }

        var ticket = new AuthenticationTicket(AccessTokens.CreateUser(owner, SchemeName), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
