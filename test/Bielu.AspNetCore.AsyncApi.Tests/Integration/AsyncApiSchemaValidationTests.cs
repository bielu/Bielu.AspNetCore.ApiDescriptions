using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Writers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Tests that validate the generated AsyncAPI schema conforms to the AsyncAPI specification
/// using ByteBard.AsyncAPI.NET reader and writer.
/// </summary>
public class AsyncApiSchemaValidationTests
{
    private static string GetDocumentRoute(string documentName) => 
        AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName);

    /// <summary>
    /// Validates that the generated AsyncAPI document contains the correct version string
    /// based on the configured AsyncApiVersion.
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
    public async Task GeneratedDocument_CanBeSerializedAndDeserializedAsV3()
    {
        // Arrange
        var expectedVersion = AsyncApiVersion.AsyncApi3_0;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = expectedVersion;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("Schema Validation Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        // Validate version is correct for V3
        ValidateAsyncApiVersion(content, expectedVersion);

        // Parse with ByteBard reader
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        // Assert - Validate the document can be parsed
        document.ShouldNotBeNull();
        
        // Re-serialize to verify round-trip
        using var stringWriter = new StringWriter();
        var jsonWriter = new AsyncApiJsonWriter(stringWriter);
        document.SerializeV3(jsonWriter);
        var reserialized = stringWriter.ToString();

        // Parse again to ensure consistency
        var reReadDocument = reader.Read(reserialized, out _);
        reReadDocument.ShouldNotBeNull();
    }

    [Fact]
    public async Task GeneratedDocument_CanBeSerializedAndDeserializedAsV2()
    {
        // Arrange
        var expectedVersion = AsyncApiVersion.AsyncApi2_0;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = expectedVersion;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("Schema Validation Test V2", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        // Validate version is correct for V2
        ValidateAsyncApiVersion(content, expectedVersion);

        // Parse with ByteBard reader
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        
        // Re-serialize to V2
        using var stringWriter = new StringWriter();
        var jsonWriter = new AsyncApiJsonWriter(stringWriter);
        document.SerializeV2(jsonWriter);
        var reserialized = stringWriter.ToString();

        reserialized.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GeneratedDocument_HasValidAsyncApiVersion()
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
        
        // Validate version using shared helper
        ValidateAsyncApiVersion(content, expectedVersion);
        
        // Additional structural validation
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;
        root.TryGetProperty("asyncapi", out var version).ShouldBeTrue();
        var versionString = version.GetString();
        versionString.ShouldNotBeNullOrEmpty();
        versionString!.ShouldStartWith("3.");
    }

    [Fact]
    public async Task GeneratedDocument_V2HasValidAsyncApiVersion()
    {
        // Arrange
        var expectedVersion = AsyncApiVersion.AsyncApi2_0;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = expectedVersion;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("V2 Version Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();
        
        // Validate version using shared helper
        ValidateAsyncApiVersion(content, expectedVersion);
        
        // Additional structural validation
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;
        root.TryGetProperty("asyncapi", out var version).ShouldBeTrue();
        var versionString = version.GetString();
        versionString.ShouldNotBeNullOrEmpty();
        versionString!.ShouldStartWith("2.");
    }

    [Fact]
    public async Task GeneratedDocument_SchemaComponentsAreValid()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("Schema Components Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        
        // Components should be valid if present
        if (document.Components?.Schemas != null)
        {
            foreach (var schema in document.Components.Schemas)
            {
                schema.Key.ShouldNotBeNullOrEmpty();
                schema.Value.ShouldNotBeNull();
            }
        }
    }

    [Fact]
    public async Task GeneratedDocument_ServersAreValid()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddServer("mqtt-server", "mqtt.example.com", "mqtt");
            options.AddServer("ws-server", "ws.example.com", "websocket");
            options.WithInfo("Multi-Server Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        document.Servers.ShouldNotBeNull();
        document.Servers.ShouldContainKey("mqtt-server");
        document.Servers.ShouldContainKey("ws-server");
        
        document.Servers["mqtt-server"].Host.ShouldBe("mqtt.example.com");
        document.Servers["mqtt-server"].Protocol.ShouldBe("mqtt");
        
        document.Servers["ws-server"].Host.ShouldBe("ws.example.com");
        document.Servers["ws-server"].Protocol.ShouldBe("websocket");
    }

    [Fact]
    public async Task GeneratedDocument_InfoIsValid()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.WithInfo("Test API Title", "2.0.0");
            options.WithDescription("This is a test API description");
            options.WithLicense("MIT", "https://opensource.org/licenses/MIT");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        document.Info.ShouldNotBeNull();
        document.Info.Title.ShouldContain("Test API Title");
        document.Info.Version.ShouldBe("2.0.0");
        document.Info.Description.ShouldBe("This is a test API description");
        document.Info.License.ShouldNotBeNull();
        document.Info.License!.Name.ShouldBe("MIT");
    }

    [Fact]
    public async Task GeneratedDocument_NoParsingErrors()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("No Errors Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        // Assert - Check there are no parsing errors
        document.ShouldNotBeNull();
        
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        if (errorCount > 0)
        {
            var errors = string.Join(Environment.NewLine, 
                diagnostic!.Errors.Select(e => $"- {e.Message}"));
            Assert.Fail($"Document has {errorCount} parsing error(s):{Environment.NewLine}{errors}");
        }
    }

    [Fact]
    public async Task GeneratedDocument_V2HasRequiredChannelsProperty()
    {
        // Arrange - AsyncAPI 2.x specification requires 'channels' property
        // See: https://www.asyncapi.com/docs/reference/specification/v2.6.0#A2SObject
        // 
        // The serialization now ensures 'channels' is always present (as empty object {})
        // even when no channels are defined via attributes.
        var expectedVersion = AsyncApiVersion.AsyncApi2_0;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = expectedVersion;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("V2 Channels Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();
        
        // Validate version
        ValidateAsyncApiVersion(content, expectedVersion);

        // Parse JSON and check for required 'channels' property
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;
        
        // AsyncAPI 2.x requires 'channels' - it should now be present
        var hasChannels = root.TryGetProperty("channels", out var channels);
        hasChannels.ShouldBeTrue("AsyncAPI 2.x document must have 'channels' property");
        
        // Validate with ByteBard reader
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);
        
        document.ShouldNotBeNull("Document should be parseable by ByteBard.AsyncAPI.NET");
        
        // Check for schema validation errors
        if (diagnostic?.Errors != null && diagnostic.Errors.Any())
        {
            var errors = string.Join(Environment.NewLine, diagnostic.Errors.Select(e => $"- {e.Message}"));
            Assert.Fail($"AsyncAPI 2.x document has validation errors:\n{errors}");
        }
    }

    [Fact]
    public async Task GeneratedDocument_V3HasValidStructure()
    {
        // Arrange - AsyncAPI 3.0 specification structure
        // See: https://www.asyncapi.com/docs/reference/specification/v3.0.0
        var expectedVersion = AsyncApiVersion.AsyncApi3_0;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = expectedVersion;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("V3 Structure Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var content = await response.Content.ReadAsStringAsync();
        
        // Output for debugging
        var prettyJson = JsonSerializer.Serialize(JsonDocument.Parse(content).RootElement, 
            new JsonSerializerOptions { WriteIndented = true });
        
        // Validate version
        ValidateAsyncApiVersion(content, expectedVersion);

        // Parse JSON and validate V3 structure
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;
        
        // AsyncAPI 3.0 required fields: asyncapi, info
        root.TryGetProperty("asyncapi", out _).ShouldBeTrue("Missing 'asyncapi' field");
        root.TryGetProperty("info", out var info).ShouldBeTrue("Missing 'info' field");
        info.TryGetProperty("title", out _).ShouldBeTrue("Missing 'info.title' field");
        info.TryGetProperty("version", out _).ShouldBeTrue("Missing 'info.version' field");
        
        // Validate with ByteBard reader
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);
        
        document.ShouldNotBeNull();
        
        // Log any errors for debugging
        if (diagnostic?.Errors != null && diagnostic.Errors.Any())
        {
            var errors = string.Join(Environment.NewLine, 
                diagnostic.Errors.Select(e => $"- {e.Message}"));
            Assert.Fail($"AsyncAPI 3.0 document has validation errors:\n{errors}\n\nGenerated document:\n{prettyJson}");
        }
    }

    private static async Task<IHost> CreateTestHostAsync(Action<AsyncApiOptions>? configureOptions = null)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers(); // Required for ApplicationPartManager
                    services.AddAsyncApi(options =>
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
