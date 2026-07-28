using Bielu.Overlay.Readers;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

public class OverlayReaderTests
{
    [Fact]
    public void Read_JsonAndYaml_ProduceEquivalentDocuments()
    {
        var fromJson = OverlayStringReader.Read("""
        {
          "overlay": "1.1.0",
          "info": { "title": "Example", "version": "1.0.0", "description": "d" },
          "extends": "https://example.com/openapi.json",
          "actions": [
            { "target": "$.info", "description": "a", "update": { "x": 1 } },
            { "target": "$.paths['/a']", "remove": true }
          ]
        }
        """);

        var fromYaml = OverlayStringReader.Read("""
        overlay: 1.1.0
        info:
          title: Example
          version: 1.0.0
          description: d
        extends: https://example.com/openapi.json
        actions:
          - target: $.info
            description: a
            update:
              x: 1
          - target: $.paths['/a']
            remove: true
        """);

        fromJson.HasErrors.ShouldBeFalse();
        fromYaml.HasErrors.ShouldBeFalse();

        foreach (var result in new[] { fromJson, fromYaml })
        {
            var doc = result.Document!;
            doc.Overlay.ShouldBe("1.1.0");
            doc.Info.Title.ShouldBe("Example");
            doc.Info.Description.ShouldBe("d");
            doc.Extends.ShouldBe("https://example.com/openapi.json");
            doc.Actions.Count.ShouldBe(2);
            doc.Actions[0].Target.ShouldBe("$.info");
            doc.Actions[0].Update!["x"]!.GetValue<int>().ShouldBe(1);
            doc.Actions[1].Remove.ShouldBeTrue();
        }
    }

    [Fact]
    public void Read_RootLevelYamlFlowMapping_StillParses()
    {
        // A YAML flow mapping is valid YAML that begins with '{', so sniffing the first character alone
        // would route it to the JSON parser and fail it. JSON is tried first, then YAML.
        var result = OverlayStringReader.Read(
            "{ overlay: 1.1.0, info: { title: t, version: 1.0.0 }, actions: [ { target: $.a, remove: true } ] }");

        result.HasErrors.ShouldBeFalse(string.Join("; ", result.Diagnostics));
        result.Document.ShouldNotBeNull();
        result.Document!.Overlay.ShouldBe("1.1.0");
        result.Document.Actions.Count.ShouldBe(1);
        result.Document.Actions[0].Remove.ShouldBeTrue();
    }

    [Fact]
    public void Read_DoesNotDisposeTheCallerSuppliedStream()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.a
            remove: true
        """));

        var result = OverlayStreamReader.Read(stream);

        result.HasErrors.ShouldBeFalse();
        stream.CanRead.ShouldBeTrue("the reader must not dispose a stream it does not own");
    }

    [Fact]
    public void Read_MalformedInput_ReturnsDiagnosticsRatherThanThrowing()
    {
        var result = OverlayStringReader.Read("{ this is not: valid json ]");

        result.Document.ShouldBeNull();
        result.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void Read_MissingRequiredFields_ReportsErrors()
    {
        var result = OverlayStringReader.Read("""
        overlay: 1.1.0
        info:
          title: Only a title
        """);

        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Path == "/info/version");
        result.Diagnostics.ShouldContain(d => d.Path == "/actions");
    }

    [Fact]
    public void Read_UnrecognizedVersion_Warns()
    {
        var result = OverlayStringReader.Read("""
        overlay: 2.0.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.info
            update: { a: 1 }
        """);

        result.HasErrors.ShouldBeFalse();
        result.Diagnostics.ShouldContain(d => d.IsWarning && d.Message.Contains("Unrecognized Overlay version"));
    }

    [Fact]
    public void Read_CapturesSpecificationExtensions()
    {
        var result = OverlayStringReader.Read("""
        overlay: 1.1.0
        x-internal-id: abc
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.info
            update: { a: 1 }
            x-note: hello
        """);

        result.HasErrors.ShouldBeFalse();
        result.Document!.Extensions!["x-internal-id"]!.GetValue<string>().ShouldBe("abc");
        result.Document.Actions[0].Extensions!["x-note"]!.GetValue<string>().ShouldBe("hello");
    }

    [Fact]
    public void Read_WrongFieldType_IsReported()
    {
        var result = OverlayStringReader.Read("""
        {
          "overlay": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "actions": [ { "target": "$.info", "remove": "yes please" } ]
        }
        """);

        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Path == "/actions/0/remove");
    }

    [Fact]
    public void Read_ActionsNotAnArray_IsReported()
    {
        var result = OverlayStringReader.Read("""
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions: nope
        """);

        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Path == "/actions");
    }
}
