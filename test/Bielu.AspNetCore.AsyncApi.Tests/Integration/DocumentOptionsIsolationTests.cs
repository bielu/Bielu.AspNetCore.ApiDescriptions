using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Each generated document gets its own copy of the servers and info configured on <see cref="AsyncApiOptions"/>.
/// The document used to hold the options' own instances, so a document transformer's edits carried into every later
/// request, and concurrent requests added security requirements to the same list at once.
/// </summary>
public class DocumentOptionsIsolationTests
{
    private const string DocumentName = "document-options-isolation";

    public sealed record IsolationEvent(string Id);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("isolation/events", Servers = ["prod"])]
        [Message(typeof(IsolationEvent), MessageId = "isolationEvent")]
        [SendOperation(OperationId = "isolationEventOperation")]
        public void Events()
        {
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_DocumentTransformerEditsServerAndInfo_DoesNotCarryIntoTheNextRequest()
    {
        // Arrange
        await using var app = await StartAsync(options => options
            .AddServer("prod", "example.com:9092", "kafka")
            .WithInfo(info =>
            {
                info.Title = "Isolation";
                info.Version = "1.0.0";
                info.License = new AsyncApiLicense { Name = "MIT" };
                info.Contact = new AsyncApiContact { Name = "Team" };
            })
            .AddDocumentTransformer((document, _, _) =>
            {
                document.Servers["prod"].Description += "!";
                document.Info.License!.Name += "+";
                document.Info.Contact!.Name += "+";
                return Task.CompletedTask;
            }));
        var provider = app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName);

        // Act
        await provider.GetAsyncApiDocumentAsync();
        var second = await provider.GetAsyncApiDocumentAsync();

        // Assert
        second.Servers["prod"].Description.ShouldBe("!");
        second.Info.License!.Name.ShouldBe("MIT+");
        second.Info.Contact!.Name.ShouldBe("Team+");

        var options = app.Services.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>().Get(DocumentName);
        options.Servers["prod"].Description.ShouldBeNull();
        options.Info!.License!.Name.ShouldBe("MIT");
    }

    [Fact]
    public async Task GetAsyncApiDocument_ConcurrentRequestsWithServerSecurity_EachGetsTheConfiguredRequirementsOnce()
    {
        // Arrange
        await using var app = await StartAsync(options => options
            .AddServer("prod", "example.com:9092", "kafka")
            .AddSecurityScheme("first", new AsyncApiSecurityScheme { Type = SecuritySchemeType.UserPassword })
            .AddSecurityScheme("second", new AsyncApiSecurityScheme { Type = SecuritySchemeType.UserPassword })
            .AddSecurityScheme("third", new AsyncApiSecurityScheme { Type = SecuritySchemeType.UserPassword })
            .AddServerSecurity("prod", "first", "second", "third"));
        var provider = app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName);

        // Act: these used to throw "Collection was modified" and grow the options' list past three entries.
        var documents = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => provider.GetAsyncApiDocumentAsync())));

        // Assert
        documents.ShouldAllBe(document => document.Servers["prod"].Security.Count == 3);
        var options = app.Services.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>().Get(DocumentName);
        (options.Servers["prod"].Security?.Count ?? 0).ShouldBe(0);
    }

    [Fact]
    public async Task GetAsyncApiDocument_InfoWithTermsTagsDocsAndExtensions_KeepsAllOfThem()
    {
        // Arrange
        var termsOfService = new Uri("https://example.com/terms");
        await using var app = await StartAsync(options => options.WithInfo(info =>
        {
            info.Title = "Isolation";
            info.Version = "1.0.0";
            info.TermsOfService = termsOfService;
            info.Tags = [new AsyncApiTag { Name = "lights" }];
            info.ExternalDocs = new AsyncApiExternalDocumentation { Url = new Uri("https://example.com/docs") };
            info.Extensions = new Dictionary<string, IAsyncApiExtension> { ["x-audience"] = new AsyncApiAny("internal") };
        }));

        // Act
        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();

        // Assert
        document.Info.TermsOfService.ShouldBe(termsOfService);
        document.Info.Tags.ShouldHaveSingleItem().Name.ShouldBe("lights");
        document.Info.ExternalDocs!.Url.ShouldBe(new Uri("https://example.com/docs"));
        document.Info.Extensions.ShouldContainKey("x-audience");
    }

    private static async Task<WebApplication> StartAsync(Action<AsyncApiOptions> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, configure);

        var app = builder.Build();
        await app.StartAsync();
        return app;
    }
}
