using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using Json.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Validates the served document against the AsyncAPI Initiative's own JSON Schema for the version it
/// declares. The schemas are vendored under <c>Conformance/asyncapi</c>; see the README there.
/// </summary>
public class AsyncApiOfficialSchemaConformanceTests
{
    [Theory]
    [InlineData(AsyncApiVersion.AsyncApi2_0, AsyncApiGeneratorConstants.DefaultDocumentName)]
    [InlineData(AsyncApiVersion.AsyncApi3_0, AsyncApiGeneratorConstants.DefaultDocumentName)]
    [InlineData(AsyncApiVersion.AsyncApi2_0, "message-attribute-headers")]
    [InlineData(AsyncApiVersion.AsyncApi3_0, "message-attribute-headers")]
    [InlineData(AsyncApiVersion.AsyncApi2_0, "message-attribute-correlation-id")]
    [InlineData(AsyncApiVersion.AsyncApi3_0, "message-attribute-correlation-id")]
    [InlineData(AsyncApiVersion.AsyncApi2_0, "attribute-external-docs")]
    [InlineData(AsyncApiVersion.AsyncApi3_0, "attribute-external-docs")]
    public async Task GetAsyncApiDocument_ServedDocument_ConformsToOfficialSchema(AsyncApiVersion version, string documentName)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers();
        builder.Services.AddAsyncApi(documentName, options =>
        {
            options.AsyncApiVersion = version;
            options.AddServer("websocket-server", "localhost", "ws");
            options.WithInfo("Schema Conformance Test", "1.0.0");
        });

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        // Act
        var json = await app.GetTestClient().GetStringAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName));
        await app.StopAsync();

        using var document = JsonDocument.Parse(json);
        var declaredVersion = document.RootElement.GetProperty("asyncapi").GetString();
        declaredVersion.ShouldNotBeNull();

        var result = LoadOfficialSchema(declaredVersion).Evaluate(
            document.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List });

        // Assert
        document.RootElement.GetProperty("channels").EnumerateObject().ShouldNotBeEmpty();
        result.IsValid.ShouldBeTrue(DescribeErrors(result));
    }

    /// <summary>
    /// Guards the test above against passing vacuously: the vendored schemas must reject documents that
    /// break a rule at the root and one that is only reachable through the schema's internal references.
    /// </summary>
    [Theory]
    [InlineData("2.6.0", """{ "asyncapi": "2.6.0", "channels": {} }""")]
    [InlineData("2.6.0", """{ "asyncapi": "2.6.0", "info": { "title": "t", "version": "1" }, "channels": { "a": { "publish": { "message": { "payload": { "type": "not-a-type" } } } } } }""")]
    [InlineData("3.1.0", """{ "asyncapi": "3.1.0" }""")]
    [InlineData("3.1.0", """{ "asyncapi": "3.1.0", "info": { "title": "t", "version": "1" }, "operations": { "a": { "action": "publish", "channel": { "$ref": "#/channels/a" } } } }""")]
    public void OfficialSchema_InvalidDocument_IsRejected(string version, string json)
    {
        // Arrange
        using var document = JsonDocument.Parse(json);

        // Act
        var result = LoadOfficialSchema(version).Evaluate(document.RootElement);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    private static JsonSchema LoadOfficialSchema(string declaredVersion)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Conformance", "asyncapi", $"{declaredVersion}.json");
        File.Exists(path).ShouldBeTrue($"No vendored AsyncAPI schema for declared version '{declaredVersion}'.");

        return JsonSchema.FromText(File.ReadAllText(path));
    }

    private static string DescribeErrors(EvaluationResults result) =>
        string.Join(
            Environment.NewLine,
            result.Details
                .Where(detail => detail.Errors is { Count: > 0 })
                .SelectMany(detail => detail.Errors!.Select(error =>
                    $"{detail.InstanceLocation} [{error.Key}] {error.Value} (schema: {detail.EvaluationPath})"))
                .Distinct());
}
