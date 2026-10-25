using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tools;

/// <summary>
/// A read-only MCP tool over the app's API descriptions. Tools are built by hand rather than from attributed methods,
/// so they need no reflection and compose with any other tools the server has.
/// </summary>
/// <param name="name">The tool's name, as agents call it.</param>
/// <param name="title">The tool's human-readable title.</param>
/// <param name="description">What the tool does, written for the agent choosing a tool.</param>
/// <param name="inputSchema">The JSON Schema of the tool's arguments.</param>
/// <param name="outputSchema">
/// The JSON Schema of the tool's structured result, for tools that return one with <see cref="Structured"/>.
/// </param>
internal abstract class ApiDescriptionTool(string name, string title, string description, string inputSchema,
    string? outputSchema = null) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = name,
        Title = title,
        Description = description,
        InputSchema = JsonDocument.Parse(inputSchema).RootElement.Clone(),
        OutputSchema = outputSchema is null ? null : JsonDocument.Parse(outputSchema).RootElement.Clone(),
        Annotations = new ToolAnnotations
        {
            Title = title,
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false,
        },
    };

    public override IReadOnlyList<object> Metadata { get; } = [];

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var arguments = request.Params?.Arguments ?? new Dictionary<string, JsonElement>();
        try
        {
            return await InvokeAsync(new ApiDescriptionCatalog(request.Services!), arguments, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A document that fails to generate is the app's problem, but the agent should see why.
            return Error($"The document could not be generated: {exception.Message}");
        }
    }

    protected abstract Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken);

    /// <summary>Reads a string argument, or returns <see langword="null"/> when it is missing or empty.</summary>
    protected static string? Argument(IDictionary<string, JsonElement> arguments, string name) =>
        arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    protected static CallToolResult Text(string text) => new() { Content = [new TextContentBlock { Text = text }] };

    protected static CallToolResult Json(JsonNode node) => Text(node.ToJsonString(Indented));

    /// <summary>
    /// A result matching the tool's output schema, as <c>structuredContent</c> and, for clients that read only the
    /// content, as the same JSON in a text block.
    /// </summary>
    protected static CallToolResult Structured(JsonObject result) => new()
    {
        Content = [new TextContentBlock { Text = result.ToJsonString(Indented) }],
        StructuredContent = JsonSerializer.SerializeToElement(result),
    };

    /// <summary>Reads an optional integer argument, or returns <paramref name="fallback"/>; clamped to the range given.</summary>
    protected static int Integer(IDictionary<string, JsonElement> arguments, string name, int fallback, int minimum, int maximum) =>
        Math.Clamp(arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : fallback, minimum, maximum);

    /// <summary>A failure the agent can act on, reported as a tool result rather than a protocol error, as MCP advises.</summary>
    protected static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
}
