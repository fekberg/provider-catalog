using System.Security.Claims;
using System.Text.Json.Nodes;
using ProviderCatalog.Catalog;

namespace ProviderCatalog.Mcp;

/// <summary>The four tools the provider catalog offers over MCP.</summary>
public sealed class CatalogTools
{
    private readonly SpecStore _specs;
    private readonly FeedbackLog _feedback;

    public CatalogTools(SpecStore specs, FeedbackLog feedback)
    {
        _specs = specs;
        _feedback = feedback;

        All =
        [
            new McpTool(
                Name: "find_provider",
                Title: "Find provider",
                Description: "Finds a delivery provider in the provider catalog by name. Returns the provider's id and the API versions of its integration spec, including which version is current.",
                InputSchema: Schema(("name", "string", "The provider's name, for example Havsgren Lines.")),
                ReadOnly: true,
                Invoke: FindProvider),

            new McpTool(
                Name: "get_spec",
                Title: "Get integration spec",
                Description: "Returns one version of a provider's integration spec: the services it offers and every tracking code its API sends, with descriptions. Use find_provider first to get the provider id.",
                InputSchema: Schema(
                    ("providerId", "string", "The provider id from find_provider, for example havsgren-lines."),
                    ("version", "integer", "The API version of the spec, for example 2.")),
                ReadOnly: true,
                Invoke: GetSpec),

            new McpTool(
                Name: "list_providers",
                Title: "List providers",
                Description: "Lists every delivery provider in the provider catalog, with the API versions of their integration specs.",
                InputSchema: Schema(),
                ReadOnly: true,
                Invoke: ListProviders),

            new McpTool(
                Name: "submit_spec_feedback",
                Title: "Submit spec feedback",
                Description: "Sends feedback about a provider's integration spec to the partnerships team that owns the catalog. Only use this when someone explicitly asks to send feedback.",
                InputSchema: Schema(
                    ("providerId", "string", "The provider id, for example havsgren-lines."),
                    ("version", "integer", "The API version the feedback is about."),
                    ("message", "string", "The feedback, written for the partnerships team.")),
                ReadOnly: false,
                Invoke: SubmitFeedback),
        ];
    }

    public IReadOnlyList<McpTool> All { get; }

    public McpTool? Find(string name) => All.FirstOrDefault(tool => tool.Name == name);

    private object FindProvider(ToolArguments arguments, ClaimsPrincipal caller)
    {
        var name = arguments.GetString("name");
        var matches = _specs.Find(name);

        return matches.Count > 0
            ? new { matches }
            : throw new ToolException($"No provider in the catalog matches '{name}'. Check the spelling of the provider's name.");
    }

    private object GetSpec(ToolArguments arguments, ClaimsPrincipal caller)
    {
        var providerId = arguments.GetString("providerId");
        var version = arguments.GetInt("version");

        if (_specs.Get(providerId, version) is { } spec)
        {
            return spec;
        }

        var provider = _specs.Providers().FirstOrDefault(p => string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ToolException($"There's no provider with the id '{providerId}'. Use find_provider to look up the id.");

        var versions = string.Join(", ", provider.Versions.Select(v => v.Version));
        throw new ToolException($"{provider.Name} has no version {version}. Available versions: {versions}.");
    }

    private object ListProviders(ToolArguments arguments, ClaimsPrincipal caller) =>
        new { providers = _specs.Providers() };

    private object SubmitFeedback(ToolArguments arguments, ClaimsPrincipal caller)
    {
        var providerId = arguments.GetString("providerId");
        var version = arguments.GetInt("version");
        var message = arguments.GetString("message");

        var spec = _specs.Get(providerId, version)
            ?? throw new ToolException($"There's no spec for '{providerId}' version {version}.");

        _feedback.Add(spec.ProviderId, spec.Version, caller.Identity?.Name ?? "unknown", message);

        return new { received = true, message = $"Thanks. The partnerships team will see this on the {spec.Provider} version {spec.Version} page." };
    }

    private static JsonObject Schema(params (string Name, string Type, string Description)[] properties)
    {
        var props = new JsonObject();

        foreach (var (name, type, description) in properties)
        {
            props[name] = new JsonObject { ["type"] = type, ["description"] = description };
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = new JsonArray(properties.Select(p => (JsonNode)p.Name).ToArray()),
        };
    }
}
