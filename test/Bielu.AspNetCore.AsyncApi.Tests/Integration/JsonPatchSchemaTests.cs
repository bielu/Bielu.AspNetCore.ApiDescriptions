using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// JSON Patch documents get a hand-written RFC 6902 schema instead of the shape of the CLR type, and every
/// JSON Patch type (plain, generic, derived) shares that one schema.
/// </summary>
public class JsonPatchSchemaTests
{
    private const string TestDocumentName = "v1";

    [Fact]
    public async Task GetAsyncApiDocument_JsonPatchProperties_ShareTheRfc6902Schema()
    {
        // Arrange
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAsyncApi(TestDocumentName);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAsyncApi());
                });
            })
            .StartAsync();
        var client = host.GetTestServer().CreateClient();

        // Act
        var response = await client.GetAsync($"/asyncapi/{TestDocumentName}.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var schemas = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .GetProperty("components").GetProperty("schemas");
        var properties = schemas.GetProperty("profilePatchMessage").GetProperty("properties");

        foreach (var name in new[] { "patch", "typedPatch" })
        {
            var patch = properties.GetProperty(name);
            patch.GetProperty("type").GetString().ShouldBe("array");
            var operations = patch.GetProperty("items").GetProperty("oneOf").EnumerateArray().ToList();
            operations.Count.ShouldBe(3);
            var opNames = operations
                .SelectMany(operation => operation.GetProperty("properties").GetProperty("op").GetProperty("enum").EnumerateArray())
                .Select(op => op.GetString())
                .ToList();
            opNames.ShouldBe(["add", "replace", "test", "move", "copy", "remove"], ignoreOrder: true);
            // Operations are closed objects; "additionalProperties": false is emitted as { "not": {} }.
            foreach (var operation in operations)
            {
                var notSchema = operation.GetProperty("additionalProperties").GetProperty("not");
                notSchema.ValueKind.ShouldBe(JsonValueKind.Object);
                notSchema.EnumerateObject().ShouldBeEmpty();
            }
        }
    }

    [Theory]
    [InlineData(typeof(JsonPatchDocument), true)]
    [InlineData(typeof(JsonPatchDocument<ProfilePatchMessage>), true)]
    [InlineData(typeof(DerivedProfilePatch), true)]
    [InlineData(typeof(ProfilePatchMessage), false)]
    [InlineData(typeof(object), false)]
    public void IsJsonPatchDocument_RecognizesPatchTypesByName(Type type, bool expected)
    {
        // Act & Assert
        type.IsJsonPatchDocument().ShouldBe(expected);
    }
}

public sealed class DerivedProfilePatch : JsonPatchDocument<ProfilePatchMessage>
{
}

[AsyncApi]
[Channel("profilePatches")]
public class ProfilePatchBus
{
    [ReceiveOperation(typeof(ProfilePatchMessage))]
    public void Publish(ProfilePatchMessage message) { }
}

public class ProfilePatchMessage
{
    public string ProfileId { get; set; } = string.Empty;

    public JsonPatchDocument Patch { get; set; } = new();

    public JsonPatchDocument<ProfilePatchMessage> TypedPatch { get; set; } = new();
}
