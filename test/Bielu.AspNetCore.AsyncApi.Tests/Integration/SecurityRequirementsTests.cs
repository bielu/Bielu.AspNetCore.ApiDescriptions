using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Operation <c>SecuritySchemes</c> and <c>AddServerSecurity</c> must produce <c>security</c> references.
/// </summary>
public class SecurityRequirementsTests
{
    private const string DocumentName = "security-requirements";

    public sealed record OrderPlaced(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("secure/orders")]
        [Message(typeof(OrderPlaced), MessageId = "securedOrder")]
        [SubscribeOperation(typeof(OrderPlaced), OperationId = "securedOperation", SecuritySchemes = ["apiKey"])]
        public void SecuredOrder()
        {
        }

        [Channel("open/orders")]
        [Message(typeof(OrderPlaced), MessageId = "openOrder")]
        [SubscribeOperation(typeof(OrderPlaced), OperationId = "openOperation")]
        public void OpenOrder()
        {
        }
    }

    internal static void Configure(AsyncApiOptions options)
    {
        options.AddServer("broker", "localhost:5672", "amqp");
        options.AddSecurityScheme("apiKey", new AsyncApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.User });
        options.AddServerSecurity("broker", "apiKey");
    }

    [Fact]
    public async Task GetAsyncApiDocument_OperationWithSecuritySchemes_ReferencesScheme()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Components.SecuritySchemes.ShouldContainKey("apiKey");
        var security = document.Operations["securedOperation"].Security;
        security.ShouldNotBeNull();
        security.Count.ShouldBe(1);
        security[0].ShouldBeOfType<AsyncApiSecuritySchemeReference>().Reference.Reference.ShouldEndWith("apiKey");
    }

    [Fact]
    public async Task GetAsyncApiDocument_OperationWithoutSecuritySchemes_HasNoSecurity()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        (document.Operations["openOperation"].Security ?? []).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsyncApiDocument_ServerWithSecurity_ReferencesScheme()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var security = document.Servers["broker"].Security;
        security.ShouldNotBeNull();
        security.Count.ShouldBe(1);
        security[0].ShouldBeOfType<AsyncApiSecuritySchemeReference>().Reference.Reference.ShouldEndWith("apiKey");
    }

    [Fact]
    public void AddSecurityScheme_NullArguments_Throw()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.AddSecurityScheme(null!, new AsyncApiSecurityScheme()));
        Should.Throw<ArgumentNullException>(() => options.AddSecurityScheme("a", null!));
        Should.Throw<ArgumentNullException>(() => options.AddServerSecurity(null!, "a"));
        Should.Throw<ArgumentNullException>(() => options.AddServerSecurity("a", null!));
    }

    private static async Task<AsyncApiDocument> GetDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, Configure);

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
