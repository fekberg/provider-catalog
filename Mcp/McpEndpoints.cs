using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProviderCatalog.Mcp;

/// <summary>
/// Maps the MCP endpoint using the Streamable HTTP transport. Every request is a POST with one
/// JSON-RPC message, and every answer is a plain JSON response.
/// </summary>
public static class McpEndpoints
{
    public static RouteGroupBuilder MapMcp(this IEndpointRouteBuilder endpoints, string pattern)
    {
        var group = endpoints.MapGroup(pattern);

        group.MapPost("", HandleAsync);

        // The catalog never sends messages on its own, so there's no event stream to open.
        group.MapMethods("", ["GET", "DELETE"], (HttpContext http) =>
        {
            http.Response.Headers.Allow = "POST";
            return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
        });

        return group;
    }

    private static async Task<IResult> HandleAsync(HttpContext http, McpServer server)
    {
        JsonNode? message;

        try
        {
            message = await JsonNode.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
        }
        catch (JsonException)
        {
            return JsonRpc(McpServer.Error(null, -32700, "The request isn't valid JSON."), StatusCodes.Status400BadRequest);
        }

        if (message is not JsonObject request)
        {
            return JsonRpc(McpServer.Error(null, -32600, "Send one JSON-RPC message per request."), StatusCodes.Status400BadRequest);
        }

        // Notifications (no id) and responses (no method) don't get an answer.
        if (!request.ContainsKey("method") || !request.ContainsKey("id"))
        {
            return Results.Accepted();
        }

        return JsonRpc(server.Handle(request, http.User), StatusCodes.Status200OK);
    }

    private static IResult JsonRpc(JsonObject body, int statusCode) =>
        Results.Content(body.ToJsonString(), "application/json", statusCode: statusCode);
}
