using System.Text.Json.Nodes;
using Bielu.Overlay.Models;
using Bielu.Overlay.Validation;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

public class OverlayValidatorTests
{
    private static OverlayDocument Build(params OverlayAction[] actions) => new()
    {
        Overlay = "1.1.0",
        Info = new OverlayInfo { Title = "t", Version = "1.0.0" },
        Actions = actions,
    };

    [Fact]
    public void Validate_WellFormedDocument_ReturnsNoDiagnostics()
    {
        var diagnostics = OverlayValidator.Validate(Build(
            new OverlayAction { Target = "$.info", Update = JsonNode.Parse("""{"a":1}""") }));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_EmptyActions_IsAnError()
    {
        var diagnostics = OverlayValidator.Validate(Build());

        diagnostics.ShouldContain(d => !d.IsWarning && d.Path == "/actions");
    }

    [Fact]
    public void Validate_MissingInfoFields_AreErrors()
    {
        var document = new OverlayDocument
        {
            Overlay = "1.1.0",
            Info = new OverlayInfo { Title = "", Version = "  " },
            Actions = [new OverlayAction { Target = "$.info", Remove = true }],
        };

        var diagnostics = OverlayValidator.Validate(document);

        diagnostics.ShouldContain(d => !d.IsWarning && d.Path == "/info/title");
        diagnostics.ShouldContain(d => !d.IsWarning && d.Path == "/info/version");
    }

    [Fact]
    public void Validate_UnparseableTarget_IsAnError()
    {
        var diagnostics = OverlayValidator.Validate(Build(
            new OverlayAction { Target = "$.$$!!nope", Remove = true }));

        diagnostics.ShouldContain(d => !d.IsWarning && d.Path == "/actions/0/target");
    }

    [Fact]
    public void Validate_CopyOn_1_0_Document_IsAnError()
    {
        var document = new OverlayDocument
        {
            Overlay = "1.0.0",
            Info = new OverlayInfo { Title = "t", Version = "1.0.0" },
            Actions = [new OverlayAction { Target = "$.a", Copy = "$.b" }],
        };

        var diagnostics = OverlayValidator.Validate(document);

        diagnostics.ShouldContain(d => !d.IsWarning && d.Path == "/actions/0/copy");
    }

    [Fact]
    public void Validate_RedundantFields_AreWarningsNotErrors()
    {
        // The spec does not make these mutually exclusive — `update` simply has no impact when outranked.
        var diagnostics = OverlayValidator.Validate(Build(
            new OverlayAction { Target = "$.a", Remove = true, Update = JsonNode.Parse("""{"a":1}""") }));

        diagnostics.ShouldNotBeEmpty();
        diagnostics.ShouldAllBe(d => d.IsWarning);
        diagnostics.ShouldContain(d => d.Message.Contains("have no effect"));
    }

    [Fact]
    public void Validate_ActionWithNoOperation_IsAWarning()
    {
        var diagnostics = OverlayValidator.Validate(Build(new OverlayAction { Target = "$.a" }));

        diagnostics.ShouldContain(d => d.IsWarning && d.Message.Contains("no effect"));
    }

    [Fact]
    public void Validate_UnrecognizedVersion_IsAWarning()
    {
        var document = new OverlayDocument
        {
            Overlay = "3.0.0",
            Info = new OverlayInfo { Title = "t", Version = "1.0.0" },
            Actions = [new OverlayAction { Target = "$.a", Remove = true }],
        };

        var diagnostics = OverlayValidator.Validate(document);

        diagnostics.ShouldContain(d => d.IsWarning && d.Path == "/overlay");
    }
}
