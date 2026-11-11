#if NET11_0_OR_GREATER
// C# 15 unions need the net11.0 target: the `union` keyword is C# 15, the default language version
// for net11.0 only, and System.Text.Json's union contract (JsonTypeInfoKind.Union) is new in .NET 11.
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// C# union types as message payloads and payload properties. System.Text.Json writes a union as its
/// active case, with no wrapper or discriminator, so the schema is an <c>anyOf</c> with one entry per
/// case type. That is the shape ASP.NET Core 11's OpenAPI generator uses too.
/// </summary>
public class UnionTypeSchemaTests
{
    private const string DocumentName = "union-types";

    public sealed record UnionCat(string Name, string Coat);

    public sealed record UnionDog(string Name, string Breed);

    public union UnionPet(UnionCat, UnionDog);

    public union UnionCode(int, string);

    public sealed class UnionEnvelope
    {
        public UnionPet Pet { get; set; }

        public UnionCode Code { get; set; }

        public UnionPet? MaybePet { get; set; }
    }

    /// <summary>A union declared the library way: <c>[Union]</c>, one constructor per case and <c>Value</c>.</summary>
    [Union]
    public readonly struct CustomResult : IUnion
    {
        public CustomResult(UnionCat value) => Value = value;

        public CustomResult(UnionDog value) => Value = value;

        public object? Value { get; }
    }

    /// <summary>
    /// The discriminated alternative to a union: a C# 15 closed hierarchy whose derived types
    /// System.Text.Json infers, so no [JsonDerivedType] list is needed.
    /// </summary>
    [System.Text.Json.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
    public closed record PaymentEvent(string PaymentId);

    public sealed record PaymentAuthorized(string PaymentId, decimal Amount) : PaymentEvent(PaymentId);

    public sealed record PaymentFailed(string PaymentId, string Reason) : PaymentEvent(PaymentId);

    public sealed class UnionPetExampleProvider : IAsyncApiMessageExampleProvider
    {
        public object GetExample() => (UnionPet)new UnionDog("Rex", "Labrador");
    }

    [AsyncApi(DocumentName)]
    public sealed class UnionChannels
    {
        [Channel("pets")]
        [SendOperation(typeof(UnionPet), OperationId = "petSent")]
        [MessageExample(Name = "dog", ProviderType = typeof(UnionPetExampleProvider))]
        public void Pet()
        {
        }

        [Channel("envelopes")]
        [SendOperation(typeof(UnionEnvelope), OperationId = "envelopeSent")]
        public void Envelope()
        {
        }

        [Channel("results")]
        [ReceiveOperation(typeof(CustomResult), OperationId = "resultReceived")]
        public void Result()
        {
        }

        [Channel("payments")]
        [SendOperation(typeof(PaymentEvent), OperationId = "paymentSent")]
        public void Payment()
        {
        }
    }

    [Theory]
    [InlineData(AsyncApiVersion.AsyncApi3_0)]
    [InlineData(AsyncApiVersion.AsyncApi2_0)]
    public async Task GetAsyncApiDocument_UnionPayload_DescribesEachCaseAsAnyOf(AsyncApiVersion version)
    {
        // Act
        var schemas = (await GetDocumentAsync(version))["components"]!["schemas"]!;

        // Assert
        var pet = schemas["unionPet"]!;
        var cases = pet["anyOf"]!.AsArray();
        cases.Count.ShouldBe(2);
        cases[0]!["properties"]!.AsObject().Select(p => p.Key).ShouldBe(["name", "coat"], ignoreOrder: true);
        cases[1]!["properties"]!.AsObject().Select(p => p.Key).ShouldBe(["name", "breed"], ignoreOrder: true);
        pet["discriminator"].ShouldBeNull();
    }

    [Fact]
    public async Task GetAsyncApiDocument_PrimitiveUnionProperty_DescribesEveryCaseType()
    {
        // Act
        var envelope = (await GetDocumentAsync(AsyncApiVersion.AsyncApi3_0))["components"]!["schemas"]!["unionEnvelope"]!;

        // Assert
        var cases = envelope["properties"]!["code"]!["anyOf"]!.AsArray();
        cases.Select(c => c!["type"]!.GetValue<string>()).ShouldBe(["integer", "string"]);
    }

    [Fact]
    public async Task GetAsyncApiDocument_NullableUnionProperty_AllowsNullAndKeepsTheCases()
    {
        // Act
        var envelope = (await GetDocumentAsync(AsyncApiVersion.AsyncApi3_0))["components"]!["schemas"]!["unionEnvelope"]!;

        // Assert
        var maybePet = envelope["properties"]!["maybePet"]!;
        maybePet["type"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldContain("null");
        maybePet["anyOf"]!.AsArray().Count.ShouldBe(2);
        var pet = envelope["properties"]!["pet"]!;
        pet["type"]!.GetValue<string>().ShouldBe("object");
    }

    [Fact]
    public async Task GetAsyncApiDocument_CustomUnionAttributeType_DescribesEachCase()
    {
        // Act
        var result = (await GetDocumentAsync(AsyncApiVersion.AsyncApi3_0))["components"]!["schemas"]!["customResult"]!;

        // Assert
        result["anyOf"]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsyncApiDocument_UnionExampleProvider_SerializesTheActiveCase()
    {
        // Act
        var messages = (await GetDocumentAsync(AsyncApiVersion.AsyncApi3_0))["components"]!["messages"]!;

        // Assert: the example is the dog itself, not a { "value": ... } wrapper.
        var payload = messages["unionPet"]!["examples"]![0]!["payload"]!;
        payload["name"]!.GetValue<string>().ShouldBe("Rex");
        payload["breed"]!.GetValue<string>().ShouldBe("Labrador");
        payload["value"].ShouldBeNull();
    }

    [Fact]
    public async Task GetAsyncApiDocument_ClosedHierarchyWithInferredPolymorphism_DescribesEveryDerivedType()
    {
        // Act
        var payment = (await GetDocumentAsync(AsyncApiVersion.AsyncApi3_0))["components"]!["schemas"]!["paymentEvent"]!;

        // Assert: unlike a union, every case carries the $type discriminator.
        var cases = payment["anyOf"]!.AsArray();
        cases.Count.ShouldBe(2);
        cases.Select(c => c!["properties"]!["$type"]!["const"]!.GetValue<string>())
            .ShouldBe(["PaymentAuthorized", "PaymentFailed"], ignoreOrder: true);
    }

    [Fact]
    public async Task SchemaTransformer_UnionPayloadAndProperties_VisitsEveryCaseType()
    {
        // Arrange
        var visitedTypes = new HashSet<Type>();

        // Act
        await GetDocumentAsync(AsyncApiVersion.AsyncApi3_0, options => options.AddSchemaTransformer((_, context, _) =>
        {
            lock (visitedTypes)
            {
                visitedTypes.Add(context.JsonTypeInfo.Type);
            }

            return Task.CompletedTask;
        }));

        // Assert: union cases get the same transformer pass as polymorphic derived types, including
        // through a Nullable<union> property.
        visitedTypes.ShouldContain(typeof(UnionPet));
        visitedTypes.ShouldContain(typeof(UnionCat));
        visitedTypes.ShouldContain(typeof(UnionDog));
        visitedTypes.ShouldContain(typeof(UnionPet?));
        visitedTypes.ShouldContain(typeof(int));
        visitedTypes.ShouldContain(typeof(CustomResult));
    }

    [Fact]
    public async Task SchemaTransformer_UnionCase_ChangesOnlyThatCase()
    {
        // Act
        var schemas = (await GetDocumentAsync(AsyncApiVersion.AsyncApi3_0, options => options.AddSchemaTransformer((schema, context, _) =>
        {
            if (context.JsonTypeInfo.Type == typeof(UnionDog))
            {
                schema.Description = "A dog.";
            }

            return Task.CompletedTask;
        })))["components"]!["schemas"]!;

        // Assert
        var cases = schemas["unionPet"]!["anyOf"]!.AsArray();
        cases[0]!["description"].ShouldBeNull();
        cases[1]!["description"]!.GetValue<string>().ShouldBe("A dog.");
    }

    private static async Task<JsonNode> GetDocumentAsync(AsyncApiVersion version, Action<AsyncApiOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, options =>
        {
            options.AsyncApiVersion = version;
            options.AddIncludedChannel("pets");
            options.AddIncludedChannel("envelopes");
            options.AddIncludedChannel("results");
            options.AddIncludedChannel("payments");
            configure?.Invoke(options);
        });

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        var json = await app.GetTestClient().GetStringAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", DocumentName));
        await app.StopAsync();

        return JsonNode.Parse(json)!;
    }
}
#endif
