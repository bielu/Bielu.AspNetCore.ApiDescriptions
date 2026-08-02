using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

public class AsyncApiMessageExampleTests
{
    private const string TestDocumentName = "v1";

    public class ExampleProvider : IAsyncApiMessageExampleProvider
    {
        public object GetExample() => new { text = "provider-example" };
    }

    public class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    [AsyncApi]
    [Channel("test")]
    public class TestBus
    {
        [MessageExample(Name = "json-example", Json = "{\"text\": \"json-example\"}")]
        [MessageExample(Name = "provider-example", ProviderType = typeof(ExampleProvider))]
        [SubscribeOperation(typeof(TestMessage), OperationId = "test")]
        public void ProcessMessage(TestMessage message) { }
    }

    [Fact]
    public async Task MessageExamples_FromAttributes_ArePopulated()
    {
        // Arrange
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(TestBus).Assembly);
                    services.AddAsyncApi(TestDocumentName, options =>
                    {
                    });
                });
                webBuilder.Configure(app => { });
            })
            .StartAsync();

        var documentProvider = host.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(TestDocumentName);

        // Act
        var document = await documentProvider.GetAsyncApiDocumentAsync();

        // Assert
        var messageKey = "testMessage";
        var message = document.Components.Messages[messageKey];
        message.Examples.ShouldNotBeNull();
        message.Examples.Count.ShouldBe(2);

        var jsonExample = message.Examples.Single(e => e.Name == "json-example");
        jsonExample.Payload.ShouldNotBeNull();

        var providerExample = message.Examples.Single(e => e.Name == "provider-example");
        providerExample.Payload.ShouldNotBeNull();
    }

    [Fact]
    public async Task MessageExamples_FromFluentApi_ArePopulated()
    {
        // Arrange
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(TestBus).Assembly);
                    services.AddAsyncApi(TestDocumentName, options =>
                    {
                        options.AddMessageExample("fluent-example", new TestMessage { Text = "fluent" }, "Fluent summary");
                    });
                });
                webBuilder.Configure(app => { });
            })
            .StartAsync();

        var documentProvider = host.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(TestDocumentName);

        // Act
        var document = await documentProvider.GetAsyncApiDocumentAsync();

        // Assert
        var messageKey = "testMessage";
        var message = document.Components.Messages[messageKey];
        message.Examples.ShouldNotBeNull();
        var fluentExample = message.Examples.Single(e => e.Name == "fluent-example");
        fluentExample.Summary.ShouldBe("Fluent summary");
    }

    [Fact]
    public async Task SetSchemaExampleFromMessageExample_WhenTrue_PopulatesSchema()
    {
        // Arrange
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(TestBus).Assembly);
                    services.AddAsyncApi(TestDocumentName, options =>
                    {
                        options.SetSchemaExampleFromMessageExample = true;
                        options.AddMessageExample("fluent-example", new TestMessage { Text = "fluent" });
                    });
                });
                webBuilder.Configure(app => { });
            })
            .StartAsync();

        var documentProvider = host.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(TestDocumentName);

        // Act
        var document = await documentProvider.GetAsyncApiDocumentAsync();

        // Assert
        var messageKey = "testMessage";
        var message = document.Components.Messages[messageKey];
        var schemaKey = "testMessage";
        var schema = (document.Components.Schemas[schemaKey].Schema as AsyncApiJsonSchema);
        schema.ShouldNotBeNull();
        schema.Examples.ShouldNotBeNull();
        schema.Examples.Count.ShouldBeGreaterThan(0);
    }
}
