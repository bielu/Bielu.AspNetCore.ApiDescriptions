// Applies an OpenAPI Overlay to an AsyncAPI document, and then to an Arazzo one.
//
// The Overlay Specification is written against OpenAPI, but its mechanism — select nodes by JSONPath,
// then merge/copy/remove — carries no OpenAPI-specific assumptions. Bielu.Overlay.NET therefore operates
// on System.Text.Json's JsonNode, which makes any JSON/YAML API description a valid target.
//
// Running the same engine over two differently-shaped specifications is the point of this sample:
//
//   AsyncAPI  channels/servers are MAPS   -> targets are plain key lookups   ($.channels.internalDebug)
//   Arazzo    workflows/steps  are ARRAYS -> targets need RFC 9535 filters   ($.workflows[?@.workflowId == '...'])

using System.Text.Json.Nodes;
using Bielu.Overlay;
using Bielu.Overlay.Readers;
using Bielu.Overlay.Validation;

var exitCode = 0;

exitCode |= Run(
    "AsyncAPI",
    "asyncapi.json",
    "public.overlay.yaml",
    SummarizeAsyncApi);

Console.WriteLine();
Console.WriteLine(new string('=', 78));
Console.WriteLine();

exitCode |= Run(
    "Arazzo",
    "arazzo.json",
    "arazzo-public.overlay.yaml",
    SummarizeArazzo);

return exitCode;

static int Run(string label, string documentFile, string overlayFile, Func<JsonNode, string> summarize)
{
    var documentPath = Path.Combine(AppContext.BaseDirectory, documentFile);
    var overlayPath = Path.Combine(AppContext.BaseDirectory, overlayFile);

    Console.WriteLine($"### {label}  ({documentFile}  +  {overlayFile})");
    Console.WriteLine();

    // ------------------------------------------------------------ read the overlay

    var read = OverlayStringReader.Read(File.ReadAllText(overlayPath));

    if (read.HasErrors)
    {
        Console.Error.WriteLine("The overlay could not be read:");
        foreach (var diagnostic in read.Diagnostics)
        {
            Console.Error.WriteLine($"  {diagnostic}");
        }

        return 1;
    }

    if (read.Document is not { } overlay)
    {
        Console.Error.WriteLine("The overlay produced no document.");
        return 1;
    }

    Console.WriteLine($"Overlay : {overlay.Info.Title}");
    Console.WriteLine($"Version : {overlay.Overlay}  ({overlay.Actions.Count} actions)");

    // ------------------------------------------------------------ validate it on its own terms

    foreach (var diagnostic in OverlayValidator.Validate(overlay))
    {
        Console.WriteLine($"  validation: {diagnostic}");
    }

    // ------------------------------------------------------------ apply it

    if (JsonNode.Parse(File.ReadAllText(documentPath)) is not { } document)
    {
        Console.Error.WriteLine($"{documentFile} did not contain a JSON document.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("--- BEFORE ---");
    Console.WriteLine(summarize(document));

    // Strict: a target matching nothing becomes an error rather than a warning. In a publishing pipeline
    // that is what you want — a silently unmatched target means the overlay has drifted out of sync with
    // the document it is supposed to transform.
    var result = OverlayApplier.Apply(document, overlay, new OverlayApplyOptions { Strict = true });

    if (result.Diagnostics.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Diagnostics:");
        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine($"  {diagnostic}");
        }
    }

    if (result.HasErrors)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Overlay application reported errors; not publishing the result.");
        return 1;
    }

    if (result.Document is not { } transformed)
    {
        Console.Error.WriteLine("Overlay application produced no document.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("--- AFTER ---");
    Console.WriteLine(summarize(transformed));

    // Apply works on a deep copy, so the caller's document is never touched and one overlay can be
    // applied to many documents in turn.
    Console.WriteLine();
    Console.WriteLine($"source document unchanged : {summarize(document).ReplaceLineEndings(" | ").Trim()}");

    return 0;
}

static string SummarizeAsyncApi(JsonNode document)
{
    var servers = document["servers"]?.AsObject().Select(s => s.Key) ?? [];
    var channels = document["channels"]?.AsObject().Select(c => c.Key) ?? [];
    var operations = document["operations"]?.AsObject().Select(o => o.Key) ?? [];

    return $"""
              title      : {document["info"]?["title"]?.GetValue<string>()}
              description: {document["info"]?["description"]?.GetValue<string>() ?? "<none>"}
              servers    : {string.Join(", ", servers)}
              channels   : {string.Join(", ", channels)}
              operations : {string.Join(", ", operations)}
            """;
}

static string SummarizeArazzo(JsonNode document)
{
    // Arazzo keys its collections as arrays of objects, so every lookup here is a pattern match rather
    // than a key access — the same shape difference that makes Arazzo overlay targets filter expressions.
    IEnumerable<string> sources = document["sourceDescriptions"] is JsonArray sourceArray
        ? sourceArray.OfType<JsonNode>()
            .Select(s => $"{s["name"]?.GetValue<string>()}({s["type"]?.GetValue<string>()})")
        : [];

    IEnumerable<string> workflows = document["workflows"] is JsonArray workflowArray
        ? workflowArray.OfType<JsonNode>().Select(w =>
        {
            IEnumerable<string> steps = w["steps"] is JsonArray stepArray
                ? stepArray.OfType<JsonNode>().Select(s => s["stepId"]?.GetValue<string>() ?? "?")
                : [];

            return $"{w["workflowId"]?.GetValue<string>()} [{string.Join(" -> ", steps)}]";
        })
        : [];

    var workflowList = string.Join(Environment.NewLine + "               ", workflows);

    return $"""
              title      : {document["info"]?["title"]?.GetValue<string>()}
              description: {document["workflows"]?[0]?["description"]?.GetValue<string>() ?? "<none on first workflow>"}
              sources    : {string.Join(", ", sources)}
              workflows  : {workflowList}
            """;
}
