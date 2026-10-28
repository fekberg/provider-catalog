using System.Security.Claims;
using System.Text.Json.Nodes;

namespace ProviderCatalog.Mcp;

/// <summary>
/// A tool the MCP server offers. Invoke returns an object that's sent back to the client as JSON.
/// </summary>
public sealed record McpTool(
    string Name,
    string Title,
    string Description,
    JsonObject InputSchema,
    bool ReadOnly,
    Func<ToolArguments, ClaimsPrincipal, object> Invoke);

/// <summary>Thrown when a tool can't do what it was asked. The message is shown to the model.</summary>
public sealed class ToolException(string message) : Exception(message);

/// <summary>Reads the arguments a client sent with a tool call.</summary>
public sealed class ToolArguments(JsonObject arguments)
{
    public string GetString(string name)
    {
        var value = arguments[name] is JsonValue node && node.TryGetValue<string>(out var text) ? text.Trim() : null;
        return string.IsNullOrEmpty(value) ? throw new ToolException($"The '{name}' argument is required.") : value;
    }

    /// <summary>Accepts both 2 and "2", since models send numbers either way.</summary>
    public int GetInt(string name)
    {
        if (arguments[name] is null)
        {
            throw new ToolException($"The '{name}' argument is required.");
        }

        if (arguments[name] is JsonValue node)
        {
            if (node.TryGetValue<int>(out var number))
            {
                return number;
            }

            if (node.TryGetValue<string>(out var text) && int.TryParse(text.Trim().TrimStart('v', 'V'), out number))
            {
                return number;
            }
        }

        throw new ToolException($"The '{name}' argument must be a whole number.");
    }

    public override string ToString() => arguments.ToJsonString();
}
