using Bielu.Overlay.Models;

namespace Bielu.AspNetCore.AsyncApi.Overlay.Tests;

/// <summary>
/// Overlay fixtures shared by the integration tests, plus a scratch directory helper so file-based
/// overlays are exercised the way a real app registers them.
/// </summary>
internal static class TestOverlays
{
    public const string RetitleYaml = """
        overlay: 1.1.0
        info:
          title: Retitle
          version: 1.0.0
        actions:
          - target: $.info
            update:
              title: Overlaid Title
        """;

    public const string SecondRetitleYaml = """
        overlay: 1.1.0
        info:
          title: Retitle again
          version: 1.0.0
        actions:
          - target: $.info
            update:
              title: Second Overlay Wins
        """;

    public const string NoMatchYaml = """
        overlay: 1.1.0
        info:
          title: Targets nothing
          version: 1.0.0
        actions:
          - target: $.thisDoesNotExist
            update:
              title: never applied
        """;

    /// <summary>Writes <paramref name="content"/> to a uniquely named file that the caller deletes.</summary>
    public static string WriteTempOverlay(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"overlay-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Builds the same retitling overlay as <see cref="RetitleYaml"/>, in memory.</summary>
    public static OverlayDocument RetitleDocument() => new()
    {
        Overlay = "1.1.0",
        Info = new OverlayInfo { Title = "Retitle", Version = "1.0.0" },
        Actions =
        [
            new OverlayAction
            {
                Target = "$.info",
                Update = System.Text.Json.Nodes.JsonNode.Parse("""{"title":"In-Memory Overlay"}""")
            }
        ]
    };
}
