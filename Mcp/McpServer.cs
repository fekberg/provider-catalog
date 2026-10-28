using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProviderCatalog.Mcp;

/// <summary>
/// Answers MCP requests (JSON-RPC 2.0). The catalog only offers tools, so it supports
/// initialize, ping, tools/list and tools/call.
/// </summary>
public sealed class McpServer(CatalogTools tools, ILogger<McpServer> logger)
{
    private static readonly string[] SupportedProtocolVersions = ["2025-11-25", "2025-06-18", "2025-03-26"];

    private static readonly JsonSerializerOptions OutputOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonObject Handle(JsonObject request, ClaimsPrincipal caller)
    {
        var id = request["id"]?.DeepClone();
        var parameters = request["params"] as JsonObject ?? [];

        try
        {
            var method = request["method"] is JsonValue value && value.TryGetValue<string>(out var name) ? name : null;

            JsonNode result = method switch
            {
                "initialize" => Initialize(parameters),
                "ping" => new JsonObject(),
                "tools/list" => ListTools(),
                "tools/call" => CallTool(parameters, caller),
                _ => throw new JsonRpcException(-32601, $"Method not found: {method}"),
            };

            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
        }
        catch (JsonRpcException e)
        {
            return Error(id, e.Code, e.Message);
        }
    }

    public static JsonObject Error(JsonNode? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };

    private static JsonObject Initialize(JsonObject parameters)
    {
        // Use the client's protocol version when we support it, otherwise offer the newest one we know.
        var requested = parameters["protocolVersion"]?.GetValue<string>();
        var version = SupportedProtocolVersions.Contains(requested) ? requested : SupportedProtocolVersions[0];

        return new JsonObject
        {
            ["protocolVersion"] = version,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "provider-catalog",
                ["title"] = "Provider catalog",
                ["version"] = "1.0.0",
            },
            ["instructions"] = "The provider catalog holds the integration specs of Globomantics Shipping's delivery providers. " +
                "Use find_provider to get a provider's id and versions, then get_spec to read a version.",
        };
    }

    private JsonObject ListTools() => new()
    {
        ["tools"] = new JsonArray(tools.All.Select(tool => (JsonNode)new JsonObject
        {
            ["name"] = tool.Name,
            ["title"] = tool.Title,
            ["description"] = tool.Description,
            ["inputSchema"] = tool.InputSchema.DeepClone(),
            ["annotations"] = new JsonObject
            {
                ["title"] = tool.Title,
                ["readOnlyHint"] = tool.ReadOnly,
                ["destructiveHint"] = false,
                ["openWorldHint"] = false,
            },
        }).ToArray()),
    };

    private JsonObject CallTool(JsonObject parameters, ClaimsPrincipal caller)
    {
        var name = parameters["name"]?.GetValue<string>()
            ?? throw new JsonRpcException(-32602, "The tool name is missing.");

        var tool = tools.Find(name)
            ?? throw new JsonRpcException(-32602, $"Unknown tool: {name}");

        var arguments = new ToolArguments(parameters["arguments"] as JsonObject ?? []);
        logger.LogInformation("{Caller} called {Tool} with {Arguments}", caller.Identity?.Name, name, arguments);

        try
        {
            var output = tool.Invoke(arguments, caller);
            return ToolResult(JsonSerializer.Serialize(output, OutputOptions), isError: false);
        }
        catch (ToolException e)
        {
            // Tool errors go back as a result, so the model can read them and try again.
            return ToolResult(e.Message, isError: true);
        }
    }

    private static JsonObject ToolResult(string text, bool isError) => new()
    {
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
        ["isError"] = isError,
    };
}

public sealed class JsonRpcException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
