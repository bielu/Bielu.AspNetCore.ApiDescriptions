using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Readers;
using Bielu.Arazzo.Writers;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoBuilderExtensionTests
{
    private static Task<ArazzoDocument> BuildAsync(Action<ArazzoOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddArazzo(options =>
        {
            options.ValidateSourceReferencesOnStartup = false;
            options.WithInfo("Test", "1.0.0");
            options.AddSourceDescription("api", "https://example.com/openapi.json", ArazzoSourceDescriptionType.OpenApi);
            configure(options);
        });
        return services.BuildServiceProvider()
            .GetRequiredKeyedService<IArazzoDocumentProvider>(ArazzoDefaults.DefaultDocumentName)
            .GetArazzoDocumentAsync();
    }

    [Fact]
    public async Task Extensions_OnEveryNewPosition_ReachTheDocumentAndRoundTrip()
    {
        // Arrange
        var document = await BuildAsync(options => options
            .WithSourceDescriptionExtension("api", "x-owner", "orders-team")
            .WithComponentsExtension("x-shared", true)
            .AddWorkflow("wf", wf => wf.Step("step", s => s
                .Operation("createOrder")
                .Payload(new JsonObject { ["id"] = null })
                .Replacement(new ArazzoPayloadReplacement
                {
                    Target = "/id",
                    Value = "$inputs.id",
                    Extensions = new Dictionary<string, JsonNode?> { ["x-note"] = "from inputs" },
                })
                .WithRequestBodyExtension("x-encoding", "utf-8")
                .SuccessCriteria(new ArazzoCriterion
                {
                    Condition = "$statusCode == 201",
                    Extensions = new Dictionary<string, JsonNode?> { ["x-why"] = "created" },
                }))));

        foreach (var serialized in new[] { ArazzoJsonWriter.Write(document), ArazzoYamlWriter.Write(document) })
        {
            // Act
            var result = ArazzoStringReader.Read(serialized);

            // Assert
            result.Diagnostics.Errors.ShouldBeEmpty();
            var read = result.Document!;
            read.SourceDescriptions[0].Extensions!["x-owner"]!.GetValue<string>().ShouldBe("orders-team");
            read.Components!.Extensions!["x-shared"]!.GetValue<bool>().ShouldBeTrue();
            var step = read.Workflows[0].Steps[0];
            step.RequestBody!.Extensions!["x-encoding"]!.GetValue<string>().ShouldBe("utf-8");
            step.RequestBody.Replacements![0].Extensions!["x-note"]!.GetValue<string>().ShouldBe("from inputs");
            step.SuccessCriteria![0].Extensions!["x-why"]!.GetValue<string>().ShouldBe("created");
        }
    }

    [Fact]
    public void WithRequestBodyExtension_Alone_StillWritesARequestBody()
    {
        // Arrange
        var options = new ArazzoOptions();

        // Act
        options.AddWorkflow("wf", wf => wf.Step("step", s => s.Operation("op").WithRequestBodyExtension("x-a", 1)));

        // Assert
        options.Workflows[0].Steps[0].RequestBody!.Extensions!.ShouldContainKey("x-a");
    }

    [Fact]
    public void WithSourceDescriptionExtension_UnknownSource_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new ArazzoOptions().WithSourceDescriptionExtension("missing", "x-a", 1));
        Should.Throw<ArgumentException>(() => new ArazzoOptions().WithSourceDescriptionExtension("", "x-a", 1));
    }

    [Fact]
    public void WithComponentsExtension_InvalidName_LeavesNoComponents()
    {
        // Arrange
        var options = new ArazzoOptions();

        // Act & Assert
        Should.Throw<ArgumentException>(() => options.WithComponentsExtension("shared", 1));
        options.Components.ShouldBeNull();
    }

    [Fact]
    public void ModelObjectOverloads_RejectInvalidArguments()
    {
        new ArazzoOptions().AddWorkflow("wf", wf => wf.Step("step", s =>
        {
            s.Operation("op");
            Should.Throw<ArgumentNullException>(() => s.SuccessCriteria((ArazzoCriterion)null!));
            Should.Throw<ArgumentException>(() => s.SuccessCriteria(new ArazzoCriterion { Condition = "" }));
            Should.Throw<ArgumentNullException>(() => s.Replacement((ArazzoPayloadReplacement)null!));
            Should.Throw<ArgumentException>(() => s.Replacement(new ArazzoPayloadReplacement { Target = "", Value = "$inputs.a" }));
            Should.Throw<ArgumentException>(() => s.Replacement(new ArazzoPayloadReplacement { Target = "/a", Value = null! }));
            Should.Throw<ArgumentException>(() => s.WithRequestBodyExtension("encoding", 1));
        }));
    }
}
