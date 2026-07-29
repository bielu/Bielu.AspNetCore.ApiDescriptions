using Bielu.Overlay.Readers;
using Bielu.Overlay.Validation;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

/// <summary>
/// Runs the OpenAPI Initiative's own overlay-document fixtures — <c>pass/</c> must be accepted,
/// <c>fail/</c> must be rejected — against <see cref="OverlayStringReader"/> plus
/// <see cref="OverlayValidator"/>, for both released spec versions.
/// </summary>
/// <remarks>
/// Upstream checks these against its JSON Schemas; we check them against a hand-written reader and
/// validator. Those are different mechanisms, so a small number of <c>fail/</c> fixtures encode
/// constraints only a schema states. Those are enumerated in <see cref="SchemaOnlyFailures"/> with a
/// reason each, rather than pattern-matched away.
/// </remarks>
public class OverlayDocumentConformanceTests
{
    /// <summary>
    /// Fixtures upstream rejects that we deliberately accept, each with the reason. A reader that keeps
    /// unknown members and coerces where it reasonably can is a different contract from a closed JSON
    /// Schema; where the difference cannot cause a wrong transformation, we do not manufacture an error.
    /// </summary>
    private static readonly Dictionary<string, string> SchemaOnlyFailures = new();

    /// <summary>
    /// Fixtures upstream accepts that we reject, with the reason. Upstream's schema only requires
    /// <c>target</c> to be a string starting with <c>$</c>; it never parses it. We do, because a target
    /// that cannot be evaluated is an overlay that silently does nothing.
    /// </summary>
    private static readonly Dictionary<string, string> KnownBadUpstreamFixtures = new()
    {
        ["v1.0/actions-traits-example.yaml"] = TraitsExampleReason,
        ["v1.1/actions-traits-example.yaml"] = TraitsExampleReason,
    };

    private const string TraitsExampleReason =
        "The spec's own traits example targets '$.paths.*.get[?@.x-oai-traits.paged]'. RFC 9535 " +
        "member-name-shorthand is 'name-first *name-char', where name-first is ALPHA / '_' / non-ASCII " +
        "and name-char adds only DIGIT — no hyphen. So '@.x-oai-traits' is not a legal shorthand and the " +
        "expression does not parse; the conformant spelling is \"@['x-oai-traits']\". Overlay 1.1.0 pins " +
        "target to RFC 9535, but upstream's schema only checks that it starts with '$', which is why the " +
        "fixture is filed under pass/. Rejecting it is correct — do not loosen target parsing for this.";

    public static TheoryData<string, string> PassFixtures() => Enumerate("pass");

    public static TheoryData<string, string> FailFixtures() => Enumerate("fail");

    private static TheoryData<string, string> Enumerate(string outcome)
    {
        var data = new TheoryData<string, string>();
        foreach (var version in new[] { "v1.0", "v1.1" })
        {
            var directory = Path.Combine(ConformancePaths.Documents, version, outcome);
            foreach (var file in Directory.GetFiles(directory, "*.yaml").OrderBy(f => f))
            {
                data.Add(version, Path.GetFileName(file));
            }
        }

        return data;
    }

    [Fact]
    public void ConformanceFixtures_ArePresent()
    {
        // Arrange / Act / Assert — a vacuous theory (zero cases) would otherwise look like a green suite.
        Directory.GetFiles(Path.Combine(ConformancePaths.Documents, "v1.0", "pass"), "*.yaml").Length.ShouldBe(12);
        Directory.GetFiles(Path.Combine(ConformancePaths.Documents, "v1.0", "fail"), "*.yaml").Length.ShouldBe(20);
        Directory.GetFiles(Path.Combine(ConformancePaths.Documents, "v1.1", "pass"), "*.yaml").Length.ShouldBe(13);
        Directory.GetFiles(Path.Combine(ConformancePaths.Documents, "v1.1", "fail"), "*.yaml").Length.ShouldBe(22);
    }

    [Theory]
    [MemberData(nameof(PassFixtures))]
    public void ValidOverlay_IsReadAndValidatedWithoutErrors(string version, string fileName)
    {
        // Arrange
        var path = Path.Combine(ConformancePaths.Documents, version, "pass", fileName);
        var key = $"{version}/{fileName}";

        // Act
        var read = OverlayStringReader.Read(File.ReadAllText(path));

        // Assert
        read.HasErrors.ShouldBeFalse($"{key} should read cleanly: {Describe(read.Diagnostics)}");
        read.Document.ShouldNotBeNull();

        var errors = OverlayValidator.Validate(read.Document!).Where(d => !d.IsWarning).ToList();

        if (KnownBadUpstreamFixtures.TryGetValue(key, out var reason))
        {
            errors.ShouldNotBeEmpty(
                $"{key} is recorded as a known-bad upstream fixture ({reason}) but now validates cleanly — remove it from KnownBadUpstreamFixtures.");
            return;
        }

        errors.ShouldBeEmpty($"{key} should validate cleanly: {Describe(errors)}");
    }

    [Theory]
    [MemberData(nameof(FailFixtures))]
    public void InvalidOverlay_IsRejected(string version, string fileName)
    {
        // Arrange
        var path = Path.Combine(ConformancePaths.Documents, version, "fail", fileName);
        var key = $"{version}/{fileName}";

        // Act
        var read = OverlayStringReader.Read(File.ReadAllText(path));
        var rejected = read.HasErrors || read.Document is null
                       || OverlayValidator.Validate(read.Document).Any(d => !d.IsWarning);

        // Assert
        if (SchemaOnlyFailures.TryGetValue(key, out var reason))
        {
            rejected.ShouldBeFalse(
                $"{key} is recorded as a schema-only failure ({reason}) but is now rejected — remove it from SchemaOnlyFailures.");
            return;
        }

        rejected.ShouldBeTrue($"{key} should have been rejected but was accepted.");
    }

    private static string Describe(IEnumerable<OverlayDiagnostic> diagnostics)
    {
        var list = diagnostics.ToList();
        return list.Count == 0 ? "(none)" : string.Join("; ", list.Select(d => d.ToString()));
    }
}
