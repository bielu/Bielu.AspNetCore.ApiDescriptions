using System.Text.Encodings.Web;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AuthenticationSchemeDocumentTransformer"/>, which projects the
/// application's registered ASP.NET Core authentication schemes into an AsyncAPI document.
/// </summary>
public class AuthenticationSchemeDocumentTransformerTests
{
    [Fact]
    public async Task Detects_scheme_and_references_it_from_servers()
    {
        var document = CreateDocument();
        var context = CreateContext(BuildProviderWithScheme("ApiKey"));

        var transformer = new AuthenticationSchemeDocumentTransformer(new AuthenticationDetectionOptions
        {
            Map = scheme => AsyncApiSecurityScheme.HttpApiKey(ParameterLocation.Query, "api_key"),
        });

        await transformer.TransformAsync(document, context, CancellationToken.None);

        document.Components!.SecuritySchemes.ShouldContainKey("ApiKey");
        document.Components.SecuritySchemes["ApiKey"].Type.ShouldBe(SecuritySchemeType.HttpApiKey);

        var reference = document.Servers!["signalr"].Security
            .OfType<AsyncApiSecuritySchemeReference>()
            .ShouldHaveSingleItem();
        reference.Reference.Reference.ShouldBe("#/components/securitySchemes/ApiKey");
    }

    [Fact]
    public async Task Skips_schemes_the_mapper_returns_null_for()
    {
        var document = CreateDocument();
        var context = CreateContext(BuildProviderWithScheme("ApiKey"));

        var transformer = new AuthenticationSchemeDocumentTransformer(new AuthenticationDetectionOptions
        {
            Map = _ => null,
        });

        await transformer.TransformAsync(document, context, CancellationToken.None);

        document.Components!.SecuritySchemes.ShouldBeEmpty();
        document.Servers!["signalr"].Security.ShouldBeEmpty();
    }

    [Fact]
    public async Task Does_not_overwrite_a_hand_authored_scheme_by_default()
    {
        var document = CreateDocument();
        var handAuthored = AsyncApiSecurityScheme.HttpApiKey(ParameterLocation.Header, "X-Custom");
        document.Components!.SecuritySchemes["ApiKey"] = handAuthored;

        var context = CreateContext(BuildProviderWithScheme("ApiKey"));

        var transformer = new AuthenticationSchemeDocumentTransformer(new AuthenticationDetectionOptions
        {
            Map = scheme => AsyncApiSecurityScheme.HttpApiKey(ParameterLocation.Query, "api_key"),
        });

        await transformer.TransformAsync(document, context, CancellationToken.None);

        // The explicit scheme survives, and it is still wired up to the server.
        document.Components.SecuritySchemes["ApiKey"].ShouldBeSameAs(handAuthored);
        document.Servers!["signalr"].Security.OfType<AsyncApiSecuritySchemeReference>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Is_a_no_op_when_authentication_is_not_registered()
    {
        var document = CreateDocument();
        var context = CreateContext(new ServiceCollection().BuildServiceProvider());

        var transformer = new AuthenticationSchemeDocumentTransformer(new AuthenticationDetectionOptions());

        await transformer.TransformAsync(document, context, CancellationToken.None);

        document.Components!.SecuritySchemes.ShouldBeEmpty();
        document.Servers!["signalr"].Security.ShouldBeEmpty();
    }

    [Fact]
    public async Task AttachToServers_false_declares_scheme_without_server_reference()
    {
        var document = CreateDocument();
        var context = CreateContext(BuildProviderWithScheme("ApiKey"));

        var transformer = new AuthenticationSchemeDocumentTransformer(new AuthenticationDetectionOptions
        {
            AttachToServers = false,
            Map = scheme => AsyncApiSecurityScheme.HttpApiKey(ParameterLocation.Query, "api_key"),
        });

        await transformer.TransformAsync(document, context, CancellationToken.None);

        document.Components!.SecuritySchemes.ShouldContainKey("ApiKey");
        document.Servers!["signalr"].Security.ShouldBeEmpty();
    }

    [Fact]
    public async Task AttachToAuthorizedOperations_secures_only_operations_of_authorized_channels()
    {
        var document = CreateDocument();
        document.Operations = new Dictionary<string, AsyncApiOperation>
        {
            ["securedOp"] = new() { Channel = new AsyncApiChannelReference("#/channels/securedChannel") },
            ["publicOp"] = new() { Channel = new AsyncApiChannelReference("#/channels/publicChannel") },
        };

        var context = CreateContext(BuildProviderWithScheme("ApiKey"), documentName: FixtureDocument);

        var transformer = new AuthenticationSchemeDocumentTransformer(new AuthenticationDetectionOptions
        {
            AttachToServers = false,
            AttachToAuthorizedOperations = true,
            Map = _ => AsyncApiSecurityScheme.HttpApiKey(ParameterLocation.Query, "api_key"),
        });

        await transformer.TransformAsync(document, context, CancellationToken.None);

        document.Servers!["signalr"].Security.ShouldBeEmpty();
        document.Operations["securedOp"].Security
            .OfType<AsyncApiSecuritySchemeReference>()
            .ShouldHaveSingleItem()
            .Reference.Reference.ShouldBe("#/components/securitySchemes/ApiKey");
        document.Operations["publicOp"].Security.ShouldBeEmpty();
    }

    [Fact]
    public async Task AttachToAuthorizedOperations_honors_explicit_authentication_schemes()
    {
        var document = CreateDocument();
        document.Operations = new Dictionary<string, AsyncApiOperation>
        {
            ["bearerOp"] = new() { Channel = new AsyncApiChannelReference("#/channels/bearerChannel") },
        };

        // Two schemes are registered, but the channel only requires "ApiKey" explicitly.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication("ApiKey")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("ApiKey", _ => { })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Other", _ => { });

        var context = CreateContext(services.BuildServiceProvider(), documentName: FixtureDocument);

        var transformer = new AuthenticationSchemeDocumentTransformer(new AuthenticationDetectionOptions
        {
            AttachToServers = false,
            AttachToAuthorizedOperations = true,
            Map = _ => AsyncApiSecurityScheme.HttpApiKey(ParameterLocation.Query, "key"),
        });

        await transformer.TransformAsync(document, context, CancellationToken.None);

        var reference = document.Operations["bearerOp"].Security
            .OfType<AsyncApiSecuritySchemeReference>()
            .ShouldHaveSingleItem();
        reference.Reference.Reference.ShouldBe("#/components/securitySchemes/ApiKey");
    }

    // The scan resolves authorization from [Authorize]/[AllowAnonymous] on the channel-declaring types
    // in this assembly. FixtureDocument scopes the scan to these fixtures only.
    private const string FixtureDocument = "auth-per-channel-fixture";

    [AsyncApi(FixtureDocument)]
    [Channel("securedChannel")]
    [Authorize]
    private sealed class SecuredChannelFixture;

    [AsyncApi(FixtureDocument)]
    [Channel("publicChannel")]
    private sealed class PublicChannelFixture;

    [AsyncApi(FixtureDocument)]
    [Channel("bearerChannel")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    private sealed class BearerChannelFixture;

    private static AsyncApiDocument CreateDocument() => new()
    {
        Asyncapi = "3.1.0",
        Components = new AsyncApiComponents { SecuritySchemes = new Dictionary<string, AsyncApiSecurityScheme>() },
        Servers = new Dictionary<string, AsyncApiServer>
        {
            ["signalr"] = new() { Host = "localhost", Protocol = "signalr" },
        },
    };

    private static AsyncApiDocumentTransformerContext CreateContext(
        IServiceProvider services, string documentName = "test") => new()
    {
        DocumentName = documentName,
        DescriptionGroups = [],
        ApplicationServices = services,
    };

    private static IServiceProvider BuildProviderWithScheme(string schemeName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(schemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(schemeName, _ => { });
        return services.BuildServiceProvider();
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());
    }
}
