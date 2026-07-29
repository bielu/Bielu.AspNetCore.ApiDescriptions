using System.Text.Json.Nodes;
using Bielu.Overlay.Readers;
using Bielu.Spec.Shared;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

/// <summary>
/// Runs the OpenAPI Initiative's own "compliant sets" — input description, overlay, expected output —
/// against <see cref="OverlayApplier"/>. These are the closest thing to an official conformance suite for
/// apply semantics, and they exercise the engine against real OpenAPI documents rather than the small
/// hand-built trees the unit tests use.
/// </summary>
public class OverlayCompliantSetsTests
{
    /// <summary>
    /// Enumerated from disk so a fixture added upstream is picked up by refreshing the vendored files,
    /// with no code change — and so a missing fixture directory fails loudly rather than silently
    /// reducing coverage.
    /// </summary>
    public static TheoryData<string> CompliantSets()
    {
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(ConformancePaths.CompliantSets).OrderBy(d => d))
        {
            data.Add(Path.GetFileName(directory));
        }

        return data;
    }

    [Fact]
    public void ConformanceFixtures_ArePresent()
    {
        // Arrange / Act
        var sets = Directory.GetDirectories(ConformancePaths.CompliantSets);

        // Assert — guards against the fixtures silently failing to copy to the output directory, which
        // would otherwise make every theory below vacuously pass with zero cases.
        sets.Length.ShouldBe(8);
    }

    [Theory]
    [MemberData(nameof(CompliantSets))]
    public void CompliantSet_ProducesTheExpectedOutput(string setName)
    {
        // Arrange
        var directory = Path.Combine(ConformancePaths.CompliantSets, setName);
        var source = ReadYaml(Path.Combine(directory, "openapi.yaml"));
        var expected = ReadYaml(Path.Combine(directory, "output.yaml"));

        var read = OverlayStringReader.Read(File.ReadAllText(Path.Combine(directory, "overlay.yaml")));
        read.HasErrors.ShouldBeFalse($"'{setName}' overlay should read cleanly: {Describe(read.Diagnostics)}");
        read.Document.ShouldNotBeNull();

        // Act
        var result = OverlayApplier.Apply(source, read.Document!, new OverlayApplyOptions { Strict = true });

        // Assert
        result.HasErrors.ShouldBeFalse($"'{setName}' should apply cleanly: {Describe(result.Diagnostics)}");

        // Structural comparison: the fixtures are YAML and we re-emit from a JsonNode tree, so key
        // ordering and formatting differ legitimately while the document does not.
        JsonNode.DeepEquals(result.Document, expected).ShouldBeTrue(
            $"'{setName}' did not match the expected output.\nExpected: {expected?.ToJsonString()}\nActual:   {result.Document?.ToJsonString()}");
    }

    private static JsonNode? ReadYaml(string path) =>
        YamlToJsonNodeConverter.Convert(new StringReader(File.ReadAllText(path)));

    private static string Describe(IReadOnlyList<OverlayDiagnostic> diagnostics) =>
        diagnostics.Count == 0 ? "(none)" : string.Join("; ", diagnostics.Select(d => d.ToString()));
}
