// Applies an OpenAPI Overlay to an AsyncAPI document.
//
// The Overlay Specification is written against OpenAPI, but its mechanism — select nodes by JSONPath,
// then merge/copy/remove — carries no OpenAPI-specific assumptions. Bielu.Overlay.NET therefore operates
// on System.Text.Json's JsonNode, which makes an AsyncAPI description just as valid a target. That is
// what this sample demonstrates.

using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Overlay;
using Bielu.Overlay.Readers;
using Bielu.Overlay.Validation;

var documentPath = Path.Combine(AppContext.BaseDirectory, "asyncapi.json");
var overlayPath = Path.Combine(AppContext.BaseDirectory, "public.overlay.yaml");

// ---------------------------------------------------------------- read the overlay

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

var overlay = read.Document!;
Console.WriteLine($"Overlay : {overlay.Info.Title}");
Console.WriteLine($"Version : {overlay.Overlay}  ({overlay.Actions.Count} actions)");

// ---------------------------------------------------------------- validate it on its own terms

var validationDiagnostics = OverlayValidator.Validate(overlay);
if (validationDiagnostics.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Validation:");
    foreach (var diagnostic in validationDiagnostics)
    {
        Console.WriteLine($"  {diagnostic}");
    }
}

// ---------------------------------------------------------------- apply it

var document = JsonNode.Parse(File.ReadAllText(documentPath))!;

Console.WriteLine();
Console.WriteLine("=== BEFORE ===");
Console.WriteLine(Summarize(document));

// Strict: a target that matches nothing becomes an error rather than a warning. In a publishing
// pipeline that is what you want — a silently unmatched target means the overlay has drifted out of
// sync with the document it is supposed to transform.
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

Console.WriteLine();
Console.WriteLine("=== AFTER ===");
Console.WriteLine(Summarize(result.Document!));

// The source document is untouched: Apply works on a deep copy, so the same overlay can be applied to
// several documents in turn without one run contaminating the next.
Console.WriteLine();
Console.WriteLine($"Source document still has 'internalDebug' channel : {document["channels"]!.AsObject().ContainsKey("internalDebug")}");
Console.WriteLine($"Source document title unchanged                   : {document["info"]!["title"]!.GetValue<string>()}");

Console.WriteLine();
Console.WriteLine("=== FULL RESULT ===");
Console.WriteLine(result.Document!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

return 0;

static string Summarize(JsonNode document)
{
    var channels = document["channels"]?.AsObject().Select(c => c.Key) ?? [];
    var servers = document["servers"]?.AsObject().Select(s => s.Key) ?? [];
    var operations = document["operations"]?.AsObject().Select(o => o.Key) ?? [];

    return $"""
              title      : {document["info"]?["title"]?.GetValue<string>()}
              description: {document["info"]?["description"]?.GetValue<string>() ?? "<none>"}
              servers    : {string.Join(", ", servers)}
              channels   : {string.Join(", ", channels)}
              operations : {string.Join(", ", operations)}
            """;
}
