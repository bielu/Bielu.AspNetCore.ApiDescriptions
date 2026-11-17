namespace Bielu.AspNetCore.AsyncApi.Versioning.Tests;

using System.Text.Json.Nodes;
using Asp.Versioning;
using Asp.Versioning.Conventions;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Models.Metadata;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Each API version's document holds only that version's declarations. The <c>[AsyncApi]</c> types below are
/// scanned for every document in this assembly, so the assertions name channels and operations by their
/// <c>vf-</c> prefix.
/// </summary>
public class ApiVersionFilteringTests
{
    private static async Task<WebApplication> StartAppAsync(Action<AsyncApiOptions, Asp.Versioning.ApiExplorer.ApiVersionDescription>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ApiVersionFilteringTests).Assembly);
        builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
            })
            // Versioned by convention only: ConventionVersionedController has no version attribute.
            .AddMvc(options => options.Conventions.Controller<ConventionVersionedController>().HasApiVersion(new ApiVersion(2, 0)))
            .AddApiExplorer(options => options.GroupNameFormat = "'v'V");
        builder.Services.AddAsyncApiForApiVersions(configure);

        var app = builder.Build();
        app.MapControllers();
        var api = app.NewVersionedApi();
        api.MapGet("/vf-minimal", MinimalApiHandlers.GetStatus).HasApiVersion(2, 0);
        app.MapAsyncApi();
        await app.StartAsync();
        return app;
    }

    private static async Task<JsonObject> GetDocumentAsync(WebApplication app, string name)
    {
        var json = await app.GetTestServer().CreateClient().GetStringAsync($"/asyncapi/{name}.json");
        return JsonNode.Parse(json)!.AsObject();
    }

    private static IReadOnlyCollection<string> Keys(JsonObject document, string property)
        => document[property] is JsonObject map ? map.Select(entry => entry.Key).ToList() : [];

    private static IReadOnlyCollection<string> ChannelMessages(JsonObject document, string channel)
        => document["channels"]?[channel]?["messages"] is JsonObject messages ? messages.Select(m => m.Key).ToList() : [];

    [Fact]
    public async Task Document_ForEachVersion_ExcludesOtherVersionsDeclarations()
    {
        // Arrange
        await using var app = await StartAppAsync();

        // Act
        var v1 = Keys(await GetDocumentAsync(app, "v1"), "channels");
        var v2 = Keys(await GetDocumentAsync(app, "v2"), "channels");

        // Assert
        v1.ShouldContain("vf-v1-only");
        v1.ShouldNotContain("vf-v2-only");
        v2.ShouldContain("vf-v2-only");
        v2.ShouldNotContain("vf-v1-only");
    }

    [Fact]
    public async Task Document_MultiVersionClass_AppliesMethodMappings()
    {
        // Arrange
        await using var app = await StartAppAsync();

        // Act
        var v1 = Keys(await GetDocumentAsync(app, "v1"), "channels");
        var v2 = Keys(await GetDocumentAsync(app, "v2"), "channels");

        // Assert
        v1.ShouldContain("vf-multi-both");
        v2.ShouldContain("vf-multi-both");
        v1.ShouldNotContain("vf-multi-v2-mapped");
        v2.ShouldContain("vf-multi-v2-mapped");
        v1.ShouldContain("vf-multi-own-v1");
        v2.ShouldNotContain("vf-multi-own-v1");
    }

    [Fact]
    public async Task Document_NeutralAndUnversionedDeclarations_AreInEveryVersion()
    {
        // Arrange
        await using var app = await StartAppAsync();

        foreach (var name in new[] { "v1", "v2" })
        {
            // Act
            var channels = Keys(await GetDocumentAsync(app, name), "channels");

            // Assert
            channels.ShouldContain("vf-neutral");
            channels.ShouldContain("vf-neutral-method");
            channels.ShouldContain("vf-unversioned");
        }
    }

    [Fact]
    public async Task Document_SharedChannelName_CarriesOnlyThatVersionsMessagesAndOperations()
    {
        // Arrange
        await using var app = await StartAppAsync();

        // Act
        var v1 = await GetDocumentAsync(app, "v1");
        var v2 = await GetDocumentAsync(app, "v2");

        // Assert
        ChannelMessages(v1, "vf-shared").ShouldBe(["sharedV1Payload"]);
        ChannelMessages(v2, "vf-shared").ShouldBe(["sharedV2Payload"]);
        Keys(v1, "operations").ShouldContain("vfSharedV1");
        Keys(v1, "operations").ShouldNotContain("vfSharedV2");
        Keys(v2, "operations").ShouldContain("vfSharedV2");
        Keys(v2, "operations").ShouldNotContain("vfSharedV1");
        Keys(v1["components"]!.AsObject(), "schemas").ShouldNotContain(key => key.Equals("sharedV2Payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Document_ClassLevelChannel_DoesNotReintroduceMethodsOfOtherVersions()
    {
        // Arrange
        await using var app = await StartAppAsync();

        // Act
        var v1 = await GetDocumentAsync(app, "v1");
        var v2 = await GetDocumentAsync(app, "v2");

        // Assert
        Keys(v1, "channels").ShouldContain("vf-class-channel");
        ChannelMessages(v1, "vf-class-channel").ShouldBe(["classChannelSharedPayload"]);
        Keys(v1, "operations").ShouldNotContain("vfClassChannelV2");
        Keys(v2, "operations").ShouldContain("vfClassChannelV2");
        ChannelMessages(v2, "vf-class-channel").ShouldBe(["classChannelSharedPayload", "classChannelV2Payload"], ignoreOrder: true);
    }

    [Fact]
    public async Task Document_ControllerVersionedByAttributeOrConvention_FollowsItsEndpointMetadata()
    {
        // Arrange
        await using var app = await StartAppAsync();

        // Act
        var v1 = Keys(await GetDocumentAsync(app, "v1"), "channels");
        var v2 = Keys(await GetDocumentAsync(app, "v2"), "channels");

        // Assert
        v1.ShouldContain("apiv1test");
        v1.ShouldNotContain("apiv2test");
        v2.ShouldContain("apiv2test");
        v2.ShouldNotContain("apiv1test");
        v1.ShouldNotContain("vf-convention");
        v2.ShouldContain("vf-convention");
    }

    [Fact]
    public async Task Document_MinimalApiHandler_FollowsItsEndpointVersion()
    {
        // Arrange
        await using var app = await StartAppAsync();

        // Act
        var v1 = Keys(await GetDocumentAsync(app, "v1"), "channels");
        var v2 = Keys(await GetDocumentAsync(app, "v2"), "channels");

        // Assert
        v1.ShouldNotContain("vf-minimal");
        v2.ShouldContain("vf-minimal");
    }

    [Fact]
    public async Task Document_ExplicitDocumentNameAndIncludedChannels_StillApply()
    {
        // Arrange
        await using var app = await StartAppAsync((options, description) =>
        {
            if (description.GroupName == "v1")
            {
                options.AddIncludedChannel("vf-v1-only").AddIncludedChannel("vf-unversioned");
            }
        });

        // Act
        var v1 = Keys(await GetDocumentAsync(app, "v1"), "channels");
        var v2 = Keys(await GetDocumentAsync(app, "v2"), "channels");

        // Assert
        v1.ShouldBe(["vf-v1-only", "vf-unversioned"], ignoreOrder: true);
        v2.ShouldContain("vf-document-v2");
        v2.ShouldNotContain("vf-v1-only");
    }

    [Fact]
    public async Task Document_GeneratedAtBuildTime_MatchesTheRuntimeDocument()
    {
        // Arrange
        await using var app = await StartAppAsync();
        var documentProvider = app.Services.GetRequiredService<IDocumentProvider>();

        foreach (var name in new[] { "v1", "v2" })
        {
            // Act
            await using var writer = new StringWriter();
            await documentProvider.GenerateAsync(name, writer);
            var buildTime = JsonNode.Parse(writer.ToString())!.AsObject();
            var runtime = await GetDocumentAsync(app, name);

            // Assert
            Keys(buildTime, "channels").ShouldBe(Keys(runtime, "channels"), ignoreOrder: true);
            Keys(buildTime, "operations").ShouldBe(Keys(runtime, "operations"), ignoreOrder: true);
        }
    }

    [Fact]
    public void Filter_RejectsNullMetadata()
    {
        // Arrange
        var filter = new ApiVersionMemberFilter(new ApiVersion(1, 0), new ApiVersionEndpointLookup(new ServiceCollection().BuildServiceProvider()));
        var type = new AsyncApiTypeMetadata(typeof(UnversionedEvents), new AsyncApiAttribute(), []);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => filter.Includes(null!, null!));
        Should.Throw<ArgumentNullException>(() => filter.Includes(type, null!));
    }

    [Fact]
    public void ScopeOf_IgnoresAdvertisedVersions()
    {
        // Act
        var scope = ApiVersionMemberFilter.ScopeOf(typeof(AdvertisedOnlyEvents));

        // Assert
        scope.ShouldBeNull();
    }
}

public sealed record V1OnlyPayload(string Id);

public sealed record V2OnlyPayload(string Id);

public sealed record MultiPayload(string Id);

public sealed record SharedV1Payload(string Id);

public sealed record SharedV2Payload(string Id, int Revision);

public sealed record ClassChannelSharedPayload(string Id);

public sealed record ClassChannelV2Payload(string Id);

[AsyncApi]
[ApiVersion("1.0")]
public class V1OnlyEvents
{
    [Channel("vf-v1-only")]
    [Message(typeof(V1OnlyPayload))]
    public void Publish()
    {
    }

    [Channel("vf-shared")]
    [Message(typeof(SharedV1Payload))]
    [SendOperation(typeof(SharedV1Payload), OperationId = "vfSharedV1")]
    public void PublishShared()
    {
    }
}

[AsyncApi]
[ApiVersion("2.0")]
public class V2OnlyEvents
{
    [Channel("vf-v2-only")]
    [Message(typeof(V2OnlyPayload))]
    public void Publish()
    {
    }

    [Channel("vf-shared")]
    [Message(typeof(SharedV2Payload))]
    [SendOperation(typeof(SharedV2Payload), OperationId = "vfSharedV2")]
    public void PublishShared()
    {
    }
}

[AsyncApi]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class MultiVersionEvents
{
    [Channel("vf-multi-both")]
    [Message(typeof(MultiPayload))]
    public void Both()
    {
    }

    [Channel("vf-multi-v2-mapped")]
    [Message(typeof(MultiPayload))]
    [MapToApiVersion("2.0")]
    public void MappedToV2()
    {
    }

    [Channel("vf-multi-own-v1")]
    [Message(typeof(MultiPayload))]
    [ApiVersion("1.0")]
    public void OwnVersion()
    {
    }

    [Channel("vf-neutral-method")]
    [Message(typeof(MultiPayload))]
    [ApiVersionNeutral]
    public void Neutral()
    {
    }
}

[AsyncApi]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Channel("vf-class-channel")]
[Message(typeof(ClassChannelSharedPayload))]
public class ClassChannelEvents
{
    [Message(typeof(ClassChannelV2Payload))]
    [SendOperation(typeof(ClassChannelV2Payload), OperationId = "vfClassChannelV2")]
    [MapToApiVersion("2.0")]
    public void PublishV2()
    {
    }
}

[AsyncApi]
[ApiVersionNeutral]
public class NeutralEvents
{
    [Channel("vf-neutral")]
    [Message(typeof(MultiPayload))]
    public void Publish()
    {
    }
}

[AsyncApi]
public class UnversionedEvents
{
    [Channel("vf-unversioned")]
    [Message(typeof(MultiPayload))]
    public void Publish()
    {
    }
}

[AsyncApi("v2")]
public class DocumentV2Events
{
    [Channel("vf-document-v2")]
    [Message(typeof(MultiPayload))]
    public void Publish()
    {
    }
}

[AsyncApi]
[AdvertiseApiVersions("3.0")]
public class AdvertisedOnlyEvents
{
}

[ApiController]
[Route("api/convention")]
[AsyncApi]
[Channel("vf-convention")]
public class ConventionVersionedController : ControllerBase
{
    [HttpGet]
    [Message(typeof(MultiPayload))]
    public IActionResult Get() => Ok("v2");
}

[AsyncApi]
public static class MinimalApiHandlers
{
    [Channel("vf-minimal")]
    [Message(typeof(MultiPayload))]
    public static string GetStatus() => "ok";
}
