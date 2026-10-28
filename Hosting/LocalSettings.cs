using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProviderCatalog.Hosting;

/// <summary>
/// Settings the catalog saves for you in catalog.local.json: the access tokens it generates on first run,
/// and the id of your dev tunnel. The file is ignored by Git.
/// </summary>
public static class LocalSettings
{
    public const string FileName = "catalog.local.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Creates two tokens when none are configured anywhere, so the catalog works on first run:
    /// "me" for your own use, and "repository" for the repository's MCP configuration.
    /// </summary>
    public static void EnsureTokens(IConfigurationManager configuration, string contentRoot)
    {
        if (configuration.GetSection("Catalog:Tokens").GetChildren().Any())
        {
            return;
        }

        var tokens = new JsonObject();

        foreach (var owner in new[] { "me", "repository" })
        {
            var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            tokens[owner] = token;
            configuration[$"Catalog:Tokens:{owner}"] = token;
        }

        Update(contentRoot, catalog => catalog["Tokens"] = tokens);
    }

    public static string? GetTunnelId(string contentRoot) =>
        Read(contentRoot)["Catalog"]?["TunnelId"]?.GetValue<string>();

    public static void SetTunnelId(string contentRoot, string? tunnelId) =>
        Update(contentRoot, catalog => catalog["TunnelId"] = tunnelId);

    private static JsonObject Read(string contentRoot)
    {
        var path = Path.Combine(contentRoot, FileName);
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [] : [];
    }

    private static void Update(string contentRoot, Action<JsonObject> change)
    {
        var settings = Read(contentRoot);

        if (settings["Catalog"] is not JsonObject catalog)
        {
            catalog = [];
            settings["Catalog"] = catalog;
        }

        change(catalog);
        File.WriteAllText(Path.Combine(contentRoot, FileName), settings.ToJsonString(WriteOptions));
    }
}
