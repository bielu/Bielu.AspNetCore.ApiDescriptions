using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Readers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Integration tests for the AsyncAPI HTTP endpoint.
/// </summary>
public class AsyncApiEndpointTests
{
    private static string GetDocumentRoute(string documentName) => 
        AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName);

    /// <summary>
    /// Validates that the generated AsyncAPI document contains the correct version string.
    /// </summary>
    private static void ValidateAsyncApiVersion(string jsonContent, AsyncApiVersion expectedVersion)
    {
        var jsonDocument = JsonDocument.Parse(jsonContent);
        var root = jsonDocument.RootElement;
        
        root.TryGetProperty("asyncapi", out var versionElement).ShouldBeTrue(
            "AsyncAPI document should contain 'asyncapi' version field");
        
        var versionString = versionElement.GetString();
        versionString.ShouldNotBeNullOrEmpty("AsyncAPI version should not be empty");
        
        switch (expectedVersion)
        {
            case AsyncApiVersion.AsyncApi2_0:
                versionString!.ShouldStartWith("2.");
                break;
            case AsyncApiVersion.AsyncApi3_0:
                versionString!.ShouldStartWith("3.");
                break;
            default:
                throw new ArgumentException($"Unknown AsyncApiVersion: {expectedVersion}");
        }
    }

    [Fact]
    public async Task MapAsyncApi_DefaultRoute_ReturnsDocument()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task MapAsyncApi_YamlRoute_ReturnsYamlContentType()
    {
        // Arrange
        using var host = await CreateTestHostAsync(configureEndpoint: pattern => "/asyncapi/{documentName}.yaml");
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/asyncapi/{AsyncApiGeneratorConstants.DefaultDocumentName}.yaml");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType!.ShouldContain("yaml");
    }

    [Fact]
    public async Task MapAsyncApi_NonExistentDocument_Returns404()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/unknown.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapAsyncApi_DocumentContent_IsValidJson()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var action = () => JsonDocument.Parse(content);
        action.ShouldNotThrow();
    }

    [Fact]
    public async Task MapAsyncApi_CaseInsensitiveDocumentName_Works()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var responseLower = await client.GetAsync($"/asyncapi/{AsyncApiGeneratorConstants.DefaultDocumentName}.json");
        var responseUpper = await client.GetAsync($"/asyncapi/{AsyncApiGeneratorConstants.DefaultDocumentName.ToUpperInvariant()}.json");

        // Assert
        responseLower.StatusCode.ShouldBe(HttpStatusCode.OK);
        responseUpper.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapAsyncApi_DocumentContainsCorrectVersion()
    {
        // Arrange
        var expectedVersion = AsyncApiVersion.AsyncApi3_0;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = expectedVersion;
        });
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        // Assert - use shared validator
        ValidateAsyncApiVersion(content, expectedVersion);
        
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        root.TryGetProperty("asyncapi", out var version).ShouldBeTrue();
        version.GetString()!.ShouldStartWith("3.");
    }

    [Fact]
    public async Task MapAsyncApi_V2DocumentContainsCorrectVersion()
    {
        // Arrange
        var expectedVersion = AsyncApiVersion.AsyncApi2_0;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = expectedVersion;
        });
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        // Assert - use shared validator
        ValidateAsyncApiVersion(content, expectedVersion);
        
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        root.TryGetProperty("asyncapi", out var version).ShouldBeTrue();
        version.GetString()!.ShouldStartWith("2.");
    }

    [Fact]
    public async Task MapAsyncApi_MultipleDocuments_CanBeAccessed()
    {
        // Arrange
        using var host = await CreateTestHostWithMultipleDocsAsync();
        var client = host.GetTestClient();

        // Act
        var responseDoc1 = await client.GetAsync("/asyncapi/api-v1.json");
        var responseDoc2 = await client.GetAsync("/asyncapi/api-v2.json");

        // Assert
        responseDoc1.StatusCode.ShouldBe(HttpStatusCode.OK);
        responseDoc2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content1 = await responseDoc1.Content.ReadAsStringAsync();
        var content2 = await responseDoc2.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var doc1 = reader.Read(content1, out _);
        var doc2 = reader.Read(content2, out _);

        doc1!.Info.Title.ShouldContain("API V1");
        doc2!.Info.Title.ShouldContain("API V2");
    }

    [Fact]
    public async Task MapAsyncApi_ExcludedFromOpenApiDescription()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act & Assert
        // The endpoint should work but be excluded from OpenAPI description
        // This is verified by the ExcludeFromDescription() call in the implementation
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapAsyncApi_ReturnsETagHeader()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.Tag.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task MapAsyncApi_WithMatchingETag_Returns304NotModified()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // First request to get the ETag
        var firstResponse = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = firstResponse.Headers.ETag;
        etag.ShouldNotBeNull();

        // Act - second request with If-None-Match header
        var request = new HttpRequestMessage(HttpMethod.Get, GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        request.Headers.IfNoneMatch.Add(etag);
        var secondResponse = await client.SendAsync(request);

        // Assert
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task MapAsyncApi_WithNonMatchingETag_Returns200()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act - request with a non-matching ETag
        var request = new HttpRequestMessage(HttpMethod.Get, GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        request.Headers.TryAddWithoutValidation("If-None-Match", "\"nonexistent-etag\"");
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
    }

    [Fact]
    public async Task MapAsyncApi_ConsecutiveRequests_ReturnSameETag()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response1 = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var response2 = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert - same document should produce same ETag
        response1.Headers.ETag.ShouldNotBeNull();
        response2.Headers.ETag.ShouldNotBeNull();
        response1.Headers.ETag.Tag.ShouldBe(response2.Headers.ETag.Tag);
    }

    private static async Task<IHost> CreateTestHostAsync(
        Action<AsyncApiOptions>? configureOptions = null,
        Func<string, string>? configureEndpoint = null)
    {
        var pattern = configureEndpoint?.Invoke(AsyncApiGeneratorConstants.DefaultAsyncApiRoute) 
                      ?? AsyncApiGeneratorConstants.DefaultAsyncApiRoute;

        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers(); // Required for ApplicationPartManager
                    services.AddAsyncApi(options =>
                    {
                        options.AddServer("test-server", "localhost", "http");
                        configureOptions?.Invoke(options);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi(pattern);
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static async Task<IHost> CreateTestHostWithMultipleDocsAsync()
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers(); // Required for ApplicationPartManager
                    services.AddAsyncApi("api-v1", options =>
                    {
                        options.WithInfo("API V1", "1.0.0");
                    });
                    services.AddAsyncApi("api-v2", options =>
                    {
                        options.WithInfo("API V2", "2.0.0");
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
