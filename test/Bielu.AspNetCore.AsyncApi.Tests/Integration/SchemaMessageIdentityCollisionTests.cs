using System.Net;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;
using SchemaIdentityFixtures = Bielu.AspNetCore.AsyncApi.Tests.Fixtures.SchemaIdentity;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Regression tests for schema/message/operation identity collision handling in
/// <see cref="AsyncApiDocumentService"/>. Each colliding scenario reproduces a case where the document
/// service previously derived a component key independently of <see cref="AsyncApiOptions.CreateSchemaReferenceId"/>
/// and silently kept the first registration whenever two unrelated payloads/operations sanitized to the same
/// key — discarding the second payload's data instead of surfacing the ambiguity.
/// </summary>
public class SchemaMessageIdentityCollisionTests
{
    /// <summary>
    /// Two unrelated payload types ("First.Event" / "Second.Event") share a short type name but live in
    /// different namespaces. With the default <see cref="AsyncApiOptions.CreateSchemaReferenceId"/>, both
    /// sanitize to the same schema/component key. Before the fix, generation silently succeeded and kept
    /// only "First.Event"'s schema, dropping "Second.Event"'s "secondOnly" property entirely. The fix must
    /// surface this as a clear, loud failure instead of silently corrupting the document.
    /// </summary>
    [Fact]
    public async Task TwoNamespaceCollision_DefaultOptions_ThrowsClearCollisionDiagnostic()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.TwoNamespaceCollision);
        var provider = host.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(
            SchemaIdentityFixtures.SchemaIdentityDocuments.TwoNamespaceCollision);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAsyncApiDocumentAsync());

        exception.Message.ShouldContain(typeof(SchemaIdentityFixtures.First.Event).FullName!);
        exception.Message.ShouldContain(typeof(SchemaIdentityFixtures.Second.Event).FullName!);
    }

    /// <summary>
    /// The same colliding types as above, but disambiguated via a custom
    /// <see cref="AsyncApiOptions.CreateSchemaReferenceId"/> (the documented escape hatch). Generation must
    /// succeed with both payloads' distinguishing properties present as separate components, and every
    /// emitted "$ref" in the served document must resolve.
    /// </summary>
    [Fact]
    public async Task TwoNamespaceCollision_WithCustomReferenceId_ProducesDistinctResolvableSchemas()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.TwoNamespaceResolved, options =>
        {
            options.CreateSchemaReferenceId = jsonTypeInfo =>
            {
                if (jsonTypeInfo.Type == typeof(SchemaIdentityFixtures.First.Event))
                    return "SchemaIdentityFirstEvent";
                if (jsonTypeInfo.Type == typeof(SchemaIdentityFixtures.Second.Event))
                    return "SchemaIdentitySecondEvent";
                return AsyncApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
            };
        });

        var document = await GetDocumentAsync(host, SchemaIdentityFixtures.SchemaIdentityDocuments.TwoNamespaceResolved);

        var firstSchema = document.Components!.Schemas!.Values
            .Select(s => s.Schema as AsyncApiJsonSchema)
            .FirstOrDefault(s => s?.Properties?.ContainsKey("firstOnly") == true);
        var secondSchema = document.Components.Schemas!.Values
            .Select(s => s.Schema as AsyncApiJsonSchema)
            .FirstOrDefault(s => s?.Properties?.ContainsKey("secondOnly") == true);

        firstSchema.ShouldNotBeNull("First.Event's schema (with its 'firstOnly' property) must not be dropped");
        secondSchema.ShouldNotBeNull("Second.Event's schema (with its 'secondOnly' property) must not be dropped");
        ReferenceEquals(firstSchema, secondSchema).ShouldBeFalse("the two unrelated types must not share one schema");

        await AssertAllReferencesResolveAsync(host, SchemaIdentityFixtures.SchemaIdentityDocuments.TwoNamespaceResolved);
    }

    /// <summary>
    /// The same payload <see cref="Type"/> referenced from two different channels is deliberate reuse, not a
    /// collision: it must keep mapping to a single shared schema/message component.
    /// </summary>
    [Fact]
    public async Task DeliberateReuse_SameTypeOnTwoChannels_ProducesSingleSharedComponent()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.DeliberateReuse);
        var document = await GetDocumentAsync(host, SchemaIdentityFixtures.SchemaIdentityDocuments.DeliberateReuse);

        // Match on the SharedNotification shape specifically (a single "message" property) rather than just
        // "has a message property", since other unrelated fixture types elsewhere in the test assembly also
        // happen to declare a "Message" property alongside others.
        var sharedSchemaCount = document.Components!.Schemas!.Values
            .Select(s => s.Schema as AsyncApiJsonSchema)
            .Count(s => s?.Properties is { Count: 1 } props && props.ContainsKey("message"));
        sharedSchemaCount.ShouldBe(1, "the same payload type used on two channels must not be split or duplicated");

        var channelA = document.Channels!.Values.Single(c => c.Address == "schema-identity/reuse-a");
        var channelB = document.Channels.Values.Single(c => c.Address == "schema-identity/reuse-b");

        channelA.Messages.Keys.Single().ShouldBe(channelB.Messages.Keys.Single());
    }

    /// <summary>
    /// Two different closed generic types over the same open generic definition (<c>Envelope&lt;T&gt;</c>)
    /// must not collapse onto the same component key. Before the fix, the schema key was derived from the
    /// bare CLR <see cref="Type.Name"/>, which for closed generic types is identical (e.g. "Envelope`1")
    /// regardless of the type argument — so both closed generics collided.
    /// </summary>
    [Fact]
    public async Task GenericPayloadTypes_DifferentTypeArguments_ProduceDistinctSchemas()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.Generics);
        var document = await GetDocumentAsync(host, SchemaIdentityFixtures.SchemaIdentityDocuments.Generics);

        var envelopeKeys = document.Components!.Schemas!.Keys
            .Where(key => key.Contains("nvelope", StringComparison.OrdinalIgnoreCase))
            .ToList();

        envelopeKeys.Count.ShouldBe(2, "Envelope<FirstEnvelopePayload> and Envelope<SecondEnvelopePayload> must each get their own component");
        envelopeKeys.Distinct().Count().ShouldBe(2);
    }

    /// <summary>
    /// Two distinct types whose custom reference ids are different strings, but collide once sanitized (and
    /// camelCased) into a component key. The collision must still be caught even though the *source* ids
    /// were not literally identical.
    /// </summary>
    [Fact]
    public async Task SanitizeCollision_DifferentCustomIdsSameSanitizedKey_ThrowsClearDiagnostic()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.SanitizeCollision, options =>
        {
            options.CreateSchemaReferenceId = jsonTypeInfo =>
            {
                if (jsonTypeInfo.Type == typeof(SchemaIdentityFixtures.SanitizeCollisionTypeA))
                    return "Sanitize Collision";
                if (jsonTypeInfo.Type == typeof(SchemaIdentityFixtures.SanitizeCollisionTypeB))
                    return "Sanitize!Collision";
                return AsyncApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
            };
        });

        var provider = host.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(
            SchemaIdentityFixtures.SchemaIdentityDocuments.SanitizeCollision);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAsyncApiDocumentAsync());

        exception.Message.ShouldContain(typeof(SchemaIdentityFixtures.SanitizeCollisionTypeA).FullName!);
        exception.Message.ShouldContain(typeof(SchemaIdentityFixtures.SanitizeCollisionTypeB).FullName!);
    }

    /// <summary>
    /// A custom <see cref="AsyncApiOptions.CreateSchemaReferenceId"/> must actually be honored for the
    /// component key used in the document, not silently ignored in favor of the CLR type name.
    /// </summary>
    [Fact]
    public async Task CustomReferenceId_IsHonoredForComponentKey()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.CustomReferenceId, options =>
        {
            options.CreateSchemaReferenceId = jsonTypeInfo =>
                jsonTypeInfo.Type == typeof(SchemaIdentityFixtures.CustomIdPayload)
                    ? "MyCustomPayloadId"
                    : AsyncApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
        });

        var document = await GetDocumentAsync(host, SchemaIdentityFixtures.SchemaIdentityDocuments.CustomReferenceId);

        document.Components!.Schemas!.Keys.ShouldContain("myCustomPayloadId");
        document.Components.Schemas.Keys.ShouldNotContain("customIdPayload");
    }

    /// <summary>
    /// When <see cref="AsyncApiOptions.CreateSchemaReferenceId"/> returns <see langword="null"/> for a type,
    /// its documented contract is that the schema is always inlined: it must not appear under
    /// components/schemas, and the message payload that uses it must embed the schema directly rather than
    /// pointing at a "$ref".
    /// </summary>
    [Fact]
    public async Task InlineSchema_WhenReferenceIdIsNull_EmbedsSchemaDirectlyInsteadOfComponentizing()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.InlineSchema, options =>
        {
            options.CreateSchemaReferenceId = jsonTypeInfo =>
                jsonTypeInfo.Type == typeof(SchemaIdentityFixtures.InlinePayload)
                    ? null
                    : AsyncApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
        });

        var document = await GetDocumentAsync(host, SchemaIdentityFixtures.SchemaIdentityDocuments.InlineSchema);

        document.Components!.Schemas!.Keys.ShouldNotContain("inlinePayload");

        var channel = document.Channels!.Values.Single(c => c.Address == "schema-identity/inline");
        var messageKey = channel.Messages.Keys.Single();
        var message = document.Components.Messages![messageKey];

        message.Payload!.Schema.ShouldNotBeOfType<AsyncApiJsonSchemaReference>();
        var inlineSchema = message.Payload.Schema as AsyncApiJsonSchema;
        inlineSchema.ShouldNotBeNull();
        inlineSchema!.Properties.ShouldNotBeNull();
        inlineSchema.Properties!.ContainsKey("value").ShouldBeTrue();
    }

    /// <summary>
    /// Two genuinely different operations (different channels, different payload types) that happen to be
    /// configured with the same explicit OperationId must be reported, not silently collapsed into one
    /// operation with the second one's data discarded.
    /// </summary>
    [Fact]
    public async Task OperationIdCollision_DifferentOperations_ThrowsClearDiagnostic()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.OperationIdCollision);
        var provider = host.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(
            SchemaIdentityFixtures.SchemaIdentityDocuments.OperationIdCollision);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAsyncApiDocumentAsync());

        exception.Message.ShouldContain("duplicateOperationId");
    }

    /// <summary>
    /// Two genuinely different messages (different payload types) that happen to share an explicit MessageId
    /// must be reported, not silently collapsed with the second message's payload discarded.
    /// </summary>
    [Fact]
    public async Task MessageIdCollision_DifferentPayloadTypes_ThrowsClearDiagnostic()
    {
        using var host = await CreateHostAsync(SchemaIdentityFixtures.SchemaIdentityDocuments.MessageIdCollision);
        var provider = host.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(
            SchemaIdentityFixtures.SchemaIdentityDocuments.MessageIdCollision);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAsyncApiDocumentAsync());

        exception.Message.ShouldContain("duplicateMessageId");
    }

    private static Task<AsyncApiDocument> GetDocumentAsync(IHost host, string documentName)
        => host.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(documentName).GetAsyncApiDocumentAsync();

    private static async Task AssertAllReferencesResolveAsync(IHost host, string documentName)
    {
        var client = host.GetTestClient();
        var route = AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName);
        var response = await client.GetAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);
        document.ShouldNotBeNull();

        if (diagnostic?.Errors != null && diagnostic.Errors.Any())
        {
            var errors = string.Join(Environment.NewLine, diagnostic.Errors.Select(e => e.Message));
            Assert.Fail($"AsyncAPI document has validation errors (a dangling '$ref' would show up here): {errors}. Generated JSON: {content}");
        }
    }

    private static async Task<IHost> CreateHostAsync(string documentName, Action<AsyncApiOptions>? configureOptions = null)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi(documentName, options =>
                    {
                        configureOptions?.Invoke(options);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
