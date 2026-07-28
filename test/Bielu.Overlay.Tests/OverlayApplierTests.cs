using System.Text.Json.Nodes;
using Bielu.Overlay;
using Bielu.Overlay.Models;
using Bielu.Overlay.Readers;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

/// <summary>
/// Semantics of <see cref="OverlayApplier"/>, driven where possible by the examples in the Overlay
/// Specification itself (§4.5) so conformance is checked against normative material rather than our own
/// reading of it.
/// </summary>
public class OverlayApplierTests
{
    private static JsonNode Doc(string json) => JsonNode.Parse(json)!;

    private static OverlayDocument Overlay(string yamlOrJson)
    {
        var result = OverlayStringReader.Read(yamlOrJson);
        result.HasErrors.ShouldBeFalse($"overlay fixture failed to parse: {string.Join("; ", result.Diagnostics)}");
        return result.Document!;
    }

    private static JsonNode ApplyOk(JsonNode document, string overlay, OverlayApplyOptions? options = null)
    {
        var result = OverlayApplier.Apply(document, Overlay(overlay), options);
        result.Diagnostics.Where(d => !d.IsWarning).ShouldBeEmpty();
        return result.Document!;
    }

    // ---------------------------------------------------------------- update

    [Fact]
    public void Update_MergesPropertiesIntoObjectTarget()
    {
        var doc = Doc("""{"info":{"title":"Example","version":"1.0.0"}}""");

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.info
            update:
              description: Added by overlay
              title: Replaced
        """);

        result["info"]!["description"]!.GetValue<string>().ShouldBe("Added by overlay");
        result["info"]!["title"]!.GetValue<string>().ShouldBe("Replaced");
        result["info"]!["version"]!.GetValue<string>().ShouldBe("1.0.0");
    }

    [Fact]
    public void Update_MergesNestedObjectsRecursively()
    {
        var doc = Doc("""{"a":{"b":{"keep":1,"change":1}}}""");

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.a
            update:
              b:
                change: 2
                added: 3
        """);

        result["a"]!["b"]!["keep"]!.GetValue<int>().ShouldBe(1);
        result["a"]!["b"]!["change"]!.GetValue<int>().ShouldBe(2);
        result["a"]!["b"]!["added"]!.GetValue<int>().ShouldBe(3);
    }

    [Fact]
    public void Update_NestedArrayReplacesRatherThanConcatenating()
    {
        // Interpretation, documented on OverlayApplier.MergeObject: the concatenate/append rule is defined
        // for a target that *selects* an array, not for arrays met partway through a recursive merge —
        // otherwise an array could never be overwritten.
        var doc = Doc("""{"a":{"tags":["old"]}}""");

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.a
            update:
              tags: [new]
        """);

        result["a"]!["tags"]!.AsArray().Count.ShouldBe(1);
        result["a"]!["tags"]![0]!.GetValue<string>().ShouldBe("new");
    }

    [Fact]
    public void Update_AppendsObjectToArrayTarget()
    {
        // Spec §4.5.4: "Array elements can be added using the update action."
        var doc = Doc("""{"paths":{"/a":{"get":{"parameters":[{"name":"top","in":"query"}]}}}}""");

        var result = ApplyOk(doc, """
        overlay: 1.0.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.paths.*.get.parameters
            update:
              name: newParam
              in: query
        """);

        var parameters = result["paths"]!["/a"]!["get"]!["parameters"]!.AsArray();
        parameters.Count.ShouldBe(2);
        parameters[1]!["name"]!.GetValue<string>().ShouldBe("newParam");
    }

    [Fact]
    public void Update_ArrayValue_ConcatenatesUnder_1_1_ButNestsUnder_1_0()
    {
        const string json = """{"tags":["a"]}""";

        var v11 = ApplyOk(Doc(json), """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.tags
            update: [b, c]
        """);

        // 1.1.0: "an array to concatenate with each selected array"
        v11["tags"]!.AsArray().Count.ShouldBe(3);
        v11["tags"]![2]!.GetValue<string>().ShouldBe("c");

        var v10 = ApplyOk(Doc(json), """
        overlay: 1.0.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.tags
            update: [b, c]
        """);

        // 1.0.0: "an entry to append to the array" — the array itself becomes one nested entry
        v10["tags"]!.AsArray().Count.ShouldBe(2);
        v10["tags"]![1]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public void Update_PrimitiveTarget_ReplacesUnder_1_1()
    {
        var doc = Doc("""{"info":{"title":"Old"}}""");

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.info.title
            update: New
        """);

        result["info"]!["title"]!.GetValue<string>().ShouldBe("New");
    }

    [Fact]
    public void Update_PrimitiveTarget_IsRejectedUnder_1_0()
    {
        // 1.0.0: "The result of the target JSONPath expression MUST be zero or more objects or arrays
        // (not primitive types or null values)."
        var result = OverlayApplier.Apply(Doc("""{"info":{"title":"Old"}}"""), Overlay("""
        overlay: 1.0.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.info.title
            update: New
        """));

        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Message.Contains("Overlay 1.0.0 does not permit"));
        result.Document!["info"]!["title"]!.GetValue<string>().ShouldBe("Old");
    }

    // ---------------------------------------------------------------- remove

    [Fact]
    public void Remove_DeletesObjectsMatchedByFilter()
    {
        // Spec §4.5.4 remove example.
        var doc = Doc("""{"paths":{"/a":{"get":{"parameters":[{"name":"top"},{"name":"dummy"}]}}}}""");

        var result = ApplyOk(doc, """
        overlay: 1.0.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.paths.*.get.parameters[?@.name == 'dummy']
            remove: true
        """);

        var parameters = result["paths"]!["/a"]!["get"]!["parameters"]!.AsArray();
        parameters.Count.ShouldBe(1);
        parameters[0]!["name"]!.GetValue<string>().ShouldBe("top");
    }

    [Fact]
    public void Remove_DeletesPrimitiveArrayItems_Under_1_1()
    {
        // Spec §4.5.4: "This also works for primitive target nodes" — new in 1.1.0.
        var doc = Doc("""{"paths":{"/a":{"get":{"tags":["public","dummy","beta"]}}}}""");

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.paths.*.get.tags[?@ == 'dummy']
            remove: true
        """);

        var tags = result["paths"]!["/a"]!["get"]!["tags"]!.AsArray();
        tags.Count.ShouldBe(2);
        tags.Select(t => t!.GetValue<string>()).ShouldBe(["public", "beta"]);
    }

    [Fact]
    public void Remove_DeletesMultipleMatchesFromTheSameArray()
    {
        // Guards the index-shifting trap: indexes are resolved live at removal time, so several matches
        // in one array stay correct regardless of the order they are returned in.
        var doc = Doc("""{"tags":["dummy","keep","dummy","dummy"]}""");

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.tags[?@ == 'dummy']
            remove: true
        """);

        result["tags"]!.AsArray().Count.ShouldBe(1);
        result["tags"]![0]!.GetValue<string>().ShouldBe("keep");
    }

    [Fact]
    public void Remove_SupportsRfc9535FilterFunctions()
    {
        // RFC 9535 defines function extensions (length, count, match, search, value). `search` is the
        // substring-regex one, and is the natural way to strip "everything internal" from a document.
        var doc = Doc("""
        {"servers":{"prod":{"host":"mqtt.example.com"},"stg":{"host":"mqtt.staging.internal"}}}
        """);

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.servers[?search(@.host, 'internal')]
            remove: true
        """);

        result["servers"]!.AsObject().ContainsKey("stg").ShouldBeFalse();
        result["servers"]!.AsObject().ContainsKey("prod").ShouldBeTrue();
    }

    [Fact]
    public void InvalidStringLiteralEscape_InFilterFunction_IsReportedAsAnInvalidPath()
    {
        // A JSONPath string literal permits only \b \f \n \r \t \/ \\ \' \" and \uXXXX, so the regex
        // `.*\.internal` must be written `.*\\.internal`. Getting this wrong is an easy authoring
        // mistake, and it must surface as a diagnostic rather than silently matching nothing.
        var result = OverlayApplier.Apply(Doc("""{"servers":{"a":{"host":"x.internal"}}}"""), Overlay("""
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.servers[?match(@.host, '.*\.internal')]
            remove: true
        """));

        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Message.Contains("not a valid RFC 9535 JSONPath expression"));
    }

    [Fact]
    public void Remove_DeletesObjectProperty()
    {
        var doc = Doc("""{"paths":{"/a":{"get":{}},"/b":{"get":{}}}}""");

        var result = ApplyOk(doc, """
        overlay: 1.0.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.paths['/b']
            remove: true
        """);

        result["paths"]!.AsObject().ContainsKey("/b").ShouldBeFalse();
        result["paths"]!.AsObject().ContainsKey("/a").ShouldBeTrue();
    }

    // ---------------------------------------------------------------- copy

    [Fact]
    public void Copy_MergesSourceNodeIntoTarget_SpecExample()
    {
        // Spec §4.5.6.1: copy all operations from the "items" path item to "some-items".
        var doc = Doc("""
        {
          "openapi": "3.1.0",
          "paths": {
            "/items":      { "get":    { "responses": { "200": { "description": "OK" } } } },
            "/some-items": { "delete": { "responses": { "200": { "description": "OK" } } } }
          }
        }
        """);

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: '$.paths["/some-items"]'
            copy: '$.paths["/items"]'
        """);

        var someItems = result["paths"]!["/some-items"]!;
        someItems["get"].ShouldNotBeNull();                 // copied in
        someItems["delete"].ShouldNotBeNull();              // copy merges, it does not replace
        result["paths"]!["/items"]!["get"].ShouldNotBeNull(); // source is left intact
    }

    [Fact]
    public void Copy_IsRejectedUnder_1_0()
    {
        var result = OverlayApplier.Apply(Doc("""{"a":{},"b":{"x":1}}"""), Overlay("""
        overlay: 1.0.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.a
            copy: $.b
        """));

        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Message.Contains("requires Overlay 1.1.0"));
    }

    [Fact]
    public void Copy_SelectingMultipleNodes_IsAnError()
    {
        var result = OverlayApplier.Apply(Doc("""{"a":{},"b":{"x":1},"c":{"y":2}}"""), Overlay("""
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.a
            copy: $['b','c']
        """));

        result.HasErrors.ShouldBeTrue();
        // Asserting the count too: "selected 0" would also satisfy "must select exactly one node", so a
        // union expression that silently matched nothing would otherwise pass this test for the wrong reason.
        result.Diagnostics.ShouldContain(d => d.Message.Contains("must select exactly one node, but selected 2"));
    }

    // ---------------------------------------------------------------- engine behaviour

    [Fact]
    public void Actions_AreAppliedSequentially_SoADeletedNodeCanBeRecreated()
    {
        // Spec §4.4.1: "Actions are applied to the result of the previous action. This enables objects to
        // be deleted in one action and then re-created in a subsequent action."
        var doc = Doc("""{"paths":{"/a":{"get":{"summary":"old"}}}}""");

        var result = ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.paths['/a']
            remove: true
          - target: $.paths
            update:
              /a:
                get: { summary: recreated }
        """);

        result["paths"]!["/a"]!["get"]!["summary"]!.GetValue<string>().ShouldBe("recreated");
    }

    [Fact]
    public void Apply_DoesNotMutateTheInputDocument()
    {
        var doc = Doc("""{"info":{"title":"Original"}}""");

        ApplyOk(doc, """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.info
            update: { title: Changed }
        """);

        doc["info"]!["title"]!.GetValue<string>().ShouldBe("Original");
    }

    [Fact]
    public void Apply_SameOverlayCanBeAppliedToSeveralDocuments()
    {
        var overlay = Overlay("""
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.info
            update: { x-owner: platform }
        """);

        var first = OverlayApplier.Apply(Doc("""{"info":{"title":"A"}}"""), overlay);
        var second = OverlayApplier.Apply(Doc("""{"info":{"title":"B"}}"""), overlay);

        first.HasErrors.ShouldBeFalse();
        second.HasErrors.ShouldBeFalse();
        first.Document!["info"]!["x-owner"]!.GetValue<string>().ShouldBe("platform");
        second.Document!["info"]!["x-owner"]!.GetValue<string>().ShouldBe("platform");
    }

    [Fact]
    public void ZeroMatchTarget_WarnsByDefault_AndErrorsUnderStrict()
    {
        var doc = Doc("""{"info":{}}""");
        const string overlay = """
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.nope
            update: { a: 1 }
        """;

        var lenient = OverlayApplier.Apply(doc, Overlay(overlay));
        lenient.HasErrors.ShouldBeFalse();
        lenient.Diagnostics.ShouldContain(d => d.IsWarning && d.Message.Contains("matched no nodes"));

        var strict = OverlayApplier.Apply(doc, Overlay(overlay), new OverlayApplyOptions { Strict = true });
        strict.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void NullValuedMatch_IsReportedAsSuch_NotAsZeroMatches()
    {
        // A JSON null has no JsonNode instance in System.Text.Json's model, so it survives JSONPath
        // selection but cannot be reached through Parent. Reporting it as "matched no nodes" would send
        // the author looking for a selector bug that isn't there.
        var result = OverlayApplier.Apply(Doc("""{"info":{"description":null}}"""), Overlay("""
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.info.description
            update: set
        """));

        result.Diagnostics.ShouldContain(d => d.Message.Contains("matched 1 JSON null value"));
        result.Diagnostics.ShouldNotContain(d => d.Message.Contains("matched no nodes"));
    }

    [Fact]
    public void Copy_SelectingASingleNullNode_IsNotReportedAsZeroSources()
    {
        var result = OverlayApplier.Apply(Doc("""{"a":{},"b":null}"""), Overlay("""
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: $.a
            copy: $.b
        """));

        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Message.Contains("selects a JSON null"));
        result.Diagnostics.ShouldNotContain(d => d.Message.Contains("selected 0"));
    }

    [Fact]
    public void InvalidTargetExpression_IsReportedAndSkipped_WithoutAbortingLaterActions()
    {
        var result = OverlayApplier.Apply(Doc("""{"info":{}}"""), Overlay("""
        overlay: 1.1.0
        info: { title: t, version: 1.0.0 }
        actions:
          - target: '$.$$!!not a path'
            update: { a: 1 }
          - target: $.info
            update: { b: 2 }
        """));

        result.HasErrors.ShouldBeTrue();
        // The second action still ran.
        result.Document!["info"]!["b"]!.GetValue<int>().ShouldBe(2);
    }

    [Fact]
    public void UnknownOverlayVersion_WarnsAndAppliesLatestSemantics()
    {
        var result = OverlayApplier.Apply(Doc("""{"tags":["a"]}"""), new OverlayDocument
        {
            Overlay = "9.9.9",
            Info = new OverlayInfo { Title = "t", Version = "1.0.0" },
            Actions = [new OverlayAction { Target = "$.tags", Update = JsonNode.Parse("""["b"]""") }],
        });

        result.Diagnostics.ShouldContain(d => d.IsWarning && d.Message.Contains("Unrecognized Overlay version"));
        result.Document!["tags"]!.AsArray().Count.ShouldBe(2); // 1.1.0 concatenation
    }

    // Targets in specifications other than OpenAPI — AsyncAPI and Arazzo — live in CrossSpecTargetTests,
    // because the map-keyed and array-keyed shapes exercise materially different code paths.
}
