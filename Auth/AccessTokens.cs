using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ProviderCatalog.Auth;

/// <summary>
/// The access tokens the catalog accepts, keyed by who each token was issued to.
/// They come from configuration (Catalog:Tokens): catalog.local.json, dotnet user-secrets, or app settings.
/// </summary>
public sealed class AccessTokens(IConfiguration configuration)
{
    private readonly Dictionary<string, string> _tokens =
        configuration.GetSection("Catalog:Tokens").Get<Dictionary<string, string>>() ?? [];

    public IReadOnlyCollection<string> Owners => _tokens.Keys;

    /// <summary>Returns the name the token was issued to, or null if the token isn't valid.</summary>
    public string? FindOwner(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var given = Encoding.UTF8.GetBytes(token);

        foreach (var (owner, expected) in _tokens)
        {
            if (CryptographicOperations.FixedTimeEquals(given, Encoding.UTF8.GetBytes(expected)))
            {
                return owner;
            }
        }

        return null;
    }

    public static ClaimsPrincipal CreateUser(string owner, string scheme) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, owner)], scheme));
}
