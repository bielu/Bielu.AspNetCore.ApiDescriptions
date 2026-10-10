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
/// <c>Traits</c> on the message and operation attributes must produce <c>traits</c> references to registered components.
/// </summary>
public class TraitsTests
{
    private const string DocumentName = "traits";

    public sealed record OrderPlaced(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("traits/orders")]
        [Message(typeof(OrderPlaced), MessageId = "tracedOrder", Traits = ["traced"])]
        [SubscribeOperation(typeof(OrderPlaced), OperationId = "tracedOperation", Traits = ["audited"])]
        public void TracedOrder()
        {
        }

        [Channel("traits/plain")]
        [Message(typeof(OrderPlaced), MessageId = "plainOrder")]
        [SubscribeOperation(typeof(OrderPlaced), OperationId = "plainOperation")]
        public void PlainOrder()
        {
        }
    }

    internal static void Configure(AsyncApiOptions options)
    {
        options.AddMessageTrait("traced", new AsyncApiMessageTrait { ContentType = "application/json" });
        options.AddOperationTrait("audited", new AsyncApiOperationTrait { Summary = "Audited operation" });
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithTraits_ReferencesRegisteredTrait()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Components.MessageTraits.ShouldContainKey("traced");
        var traits = document.Components.Messages["tracedOrder"].Traits;
        traits.ShouldNotBeNull();
        traits.Count.ShouldBe(1);
        traits[0].ShouldBeOfType<AsyncApiMessageTraitReference>().Reference.Reference.ShouldEndWith("traced");
    }

    [Fact]
    public async Task GetAsyncApiDocument_OperationWithTraits_ReferencesRegisteredTrait()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Components.OperationTraits.ShouldContainKey("audited");
        var traits = document.Operations["tracedOperation"].Traits;
        traits.ShouldNotBeNull();
        traits.Count.ShouldBe(1);
        traits[0].ShouldBeOfType<AsyncApiOperationTraitReference>().Reference.Reference.ShouldEndWith("audited");
    }

    [Fact]
    public async Task GetAsyncApiDocument_WithoutTraits_LeavesTraitsEmpty()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        (document.Components.Messages["plainOrder"].Traits ?? []).ShouldBeEmpty();
        (document.Operations["plainOperation"].Traits ?? []).ShouldBeEmpty();
    }

    [Fact]
    public void AddTrait_NullArguments_Throw()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.AddMessageTrait(null!, new AsyncApiMessageTrait()));
        Should.Throw<ArgumentNullException>(() => options.AddMessageTrait("a", null!));
        Should.Throw<ArgumentNullException>(() => options.AddOperationTrait(null!, new AsyncApiOperationTrait()));
        Should.Throw<ArgumentNullException>(() => options.AddOperationTrait("a", null!));
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
