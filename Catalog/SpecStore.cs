using System.Text.Json;

namespace ProviderCatalog.Catalog;

/// <summary>
/// Reads every spec from the data folder at startup: data/&lt;provider id&gt;/&lt;version&gt;.json.
/// </summary>
public sealed class SpecStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<ProviderSpec> _specs;

    public SpecStore(IWebHostEnvironment environment)
    {
        var dataFolder = Path.Combine(environment.ContentRootPath, "data");

        _specs = Directory
            .EnumerateFiles(dataFolder, "*.json", SearchOption.AllDirectories)
            .Select(file => JsonSerializer.Deserialize<ProviderSpec>(File.ReadAllText(file), JsonOptions)
                ?? throw new InvalidDataException($"{file} is empty."))
            .OrderBy(spec => spec.Provider)
            .ThenBy(spec => spec.Version)
            .ToList();
    }

    public IReadOnlyList<ProviderSummary> Providers() =>
        _specs
            .GroupBy(spec => spec.ProviderId)
            .Select(group => new ProviderSummary(
                group.Key,
                group.First().Provider,
                group.Select(spec => new VersionSummary(spec.Version, spec.Status, spec.Published)).ToList()))
            .ToList();

    /// <summary>Finds providers whose name or id contains the search text, ignoring case, spaces and hyphens.</summary>
    public IReadOnlyList<ProviderSummary> Find(string name)
    {
        var wanted = Normalize(name);

        return Providers()
            .Where(provider => wanted.Length > 0
                && (Normalize(provider.Name).Contains(wanted) || Normalize(provider.Id).Contains(wanted)))
            .ToList();
    }

    public ProviderSpec? Get(string providerId, int version) =>
        _specs.FirstOrDefault(spec =>
            string.Equals(spec.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) && spec.Version == version);

    private static string Normalize(string text) =>
        new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
