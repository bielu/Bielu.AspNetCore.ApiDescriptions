using System.IO.Pipelines;
using System.Text.Json.Serialization;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Schema shapes the attribute-driven fixtures never produce: binary content, JSON Patch documents, and types that
/// refer to themselves through dictionaries and polymorphic collections.
/// </summary>
public class SchemaShapeTests
{
    public sealed record Upload(Stream Body, IFormFile File, IFormFileCollection Files, PipeReader Reader);

    public sealed record PatchRequest(JsonPatchDocument Patch, JsonPatchDocument<Upload> TypedPatch);

    public sealed record TreeNode(string Name, Dictionary<string, TreeNode> Children, TreeNode? Parent);

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(Leaf), "leaf")]
    [JsonDerivedType(typeof(Group), "group")]
    public abstract record Shape;

    public sealed record Leaf(int Size) : Shape;

    public sealed record Group(List<Shape> Members) : Shape;

    [Fact]
    public async Task GetOrCreateSchemaAsync_BinaryContent_IsDescribedAsBinaryStrings()
    {
        // Act
        var schema = await GetSchemaAsync(typeof(Upload));

        // Assert
        foreach (var name in new[] { "body", "file", "reader" })
        {
            var property = schema.Properties[name].ShouldBeOfType<AsyncApiJsonSchema>();
            property.Type.ShouldBe(SchemaType.String);
            property.Format.ShouldBe("binary");
        }

        var files = schema.Properties["files"].ShouldBeOfType<AsyncApiJsonSchema>();
        files.Type.ShouldBe(SchemaType.Array);
        files.Items.ShouldNotBeNull().Format.ShouldBe("binary");
    }

    [Fact]
    public async Task GetOrCreateSchemaAsync_JsonPatchDocument_IsDescribedAsAnArrayOfOperations()
    {
        // Act
        var schema = await GetSchemaAsync(typeof(PatchRequest));

        // Assert
        foreach (var name in new[] { "patch", "typedPatch" })
        {
            var patch = schema.Properties[name].ShouldBeOfType<AsyncApiJsonSchema>();
            patch.Type.ShouldBe(SchemaType.Array);
            patch.Items.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task GetOrCreateSchemaAsync_TypeReferringToItselfThroughADictionary_Completes()
    {
        // Act
        var schema = await GetSchemaAsync(typeof(TreeNode));

        // Assert
        schema.Properties.ShouldContainKey("name");
        schema.Properties["children"].ShouldNotBeNull();
        schema.Properties["parent"].ShouldNotBeNull();
    }

    [Fact]
    public async Task GetOrCreateSchemaAsync_PolymorphicTypeContainingItself_Completes()
    {
        // Act
        var schema = await GetSchemaAsync(typeof(Group));

        // Assert
        schema.Properties["members"].ShouldBeOfType<AsyncApiJsonSchema>().Items.ShouldNotBeNull();
    }

    private static async Task<AsyncApiJsonSchema> GetSchemaAsync(Type type)
    {
        // Arrange
        AsyncApiJsonSchema? captured = null;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(options => options.AddDocumentTransformer(async (_, context, cancellationToken) =>
            captured = await context.GetOrCreateSchemaAsync(type, cancellationToken: cancellationToken)));

        await using var app = builder.Build();
        await app.StartAsync();

        await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(AsyncApiGeneratorConstants.DefaultDocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return captured.ShouldNotBeNull();
    }
}
