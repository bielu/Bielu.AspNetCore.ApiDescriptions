using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Wolverine;
using Wolverine.Kafka;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Integration;

/// <summary>
/// <c>[Message]</c> placed on a Wolverine message class: the values it declares reach the message the
/// transformer builds, the options-based metadata wins over it, and the wire name stays the component key.
/// </summary>
public class WolverineMessageAttributeTests
{
    private const string InvoiceKey = "billing.invoice-issued";

    [Fact]
    public async Task TransformAsync_MessageClassWithAttribute_AppliesAttributeValues()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var message = document["components"]!["messages"]![InvoiceKey]!;
        message["name"]!.GetValue<string>().ShouldBe("Invoice issued (display)");
        message["title"]!.GetValue<string>().ShouldBe("Invoice issued");
        message["summary"]!.GetValue<string>().ShouldBe("Raised when an invoice is issued.");
        message["description"]!.GetValue<string>().ShouldBe("Attribute description.");
        message["contentType"]!.GetValue<string>().ShouldBe("application/vnd.invoice+json");
        message["correlationId"]!["location"]!.GetValue<string>().ShouldBe("$message.payload#/invoiceId");
        message["correlationId"]!["description"]!.GetValue<string>().ShouldBe("The invoice id.");
        message["externalDocs"]!["url"]!.GetValue<string>().ShouldBe("https://example.com/invoices");
        message["externalDocs"]!["description"]!.GetValue<string>().ShouldBe("Invoice docs");
        message["traits"]!.AsArray().Single()!["$ref"]!.GetValue<string>().ShouldBe("#/components/messageTraits/traced");
    }

    [Fact]
    public async Task TransformAsync_AttributeWithNameAndMessageId_KeepsWolverineWireNameAsComponentKey()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert: the key (and the operation id derived from it) is the [MessageIdentity] name; MessageId
        // and Name must not rename what is on the wire.
        document["components"]!["messages"]!.AsObject().ContainsKey(InvoiceKey).ShouldBeTrue();
        document["components"]!["messages"]!.AsObject().ContainsKey("ignored-for-the-key").ShouldBeFalse();
        document["operations"]!.AsObject().ContainsKey($"send.{InvoiceKey}").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_AttributeTags_AreAppliedAndRegistered()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var tags = document["components"]!["messages"]!["billing.credit-note-issued"]!["tags"]!.AsArray()
            .Select(t => t!["name"]!.GetValue<string>());
        tags.ShouldBe(["finance", "billing"]);
        document["components"]!["tags"]!["finance"].ShouldNotBeNull();
    }

    [Fact]
    public async Task TransformAsync_OptionsExternalDocs_OverrideAttributeExternalDocs()
    {
        // Act
        var document = await GetDocumentAsync(wolverine =>
            wolverine.WithExternalDocs<InvoiceIssued>("https://example.com/override", "From options"));

        // Assert
        var docs = document["components"]!["messages"]![InvoiceKey]!["externalDocs"]!;
        docs["url"]!.GetValue<string>().ShouldBe("https://example.com/override");
        docs["description"]!.GetValue<string>().ShouldBe("From options");
    }

    [Fact]
    public async Task TransformAsync_OptionsTraits_AreAddedToAttributeTraits()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.WithMessageTraits<InvoiceIssued>("traced", "audited-message"));

        // Assert: "traced" comes from both the attribute and the options but is referenced once.
        var refs = document["components"]!["messages"]![InvoiceKey]!["traits"]!.AsArray()
            .Select(t => t!["$ref"]!.GetValue<string>());
        refs.ShouldBe(["#/components/messageTraits/traced", "#/components/messageTraits/audited-message"]);
    }

    [Fact]
    public async Task TransformAsync_IncludeCorrelationId_DoesNotOverrideAttributeCorrelationId()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeCorrelationId());

        // Assert
        var messages = document["components"]!["messages"]!;
        messages[InvoiceKey]!["correlationId"]!["location"]!.GetValue<string>().ShouldBe("$message.payload#/invoiceId");
        messages["orders.order-placed"]!["correlationId"]!["location"]!.GetValue<string>().ShouldBe("$message.header#/correlation-id");
    }

    [Fact]
    public async Task TransformAsync_MessageClassWithoutAttribute_IsUnchanged()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var message = document["components"]!["messages"]!["orders.order-placed"]!.AsObject();
        message["name"]!.GetValue<string>().ShouldBe("orders.order-placed");
        message["title"]!.GetValue<string>().ShouldBe("OrderPlaced");
        message["summary"]!.GetValue<string>().ShouldBe("Published to Kafka when an order is placed.");
        message.ContainsKey("externalDocs").ShouldBeFalse();
        message.ContainsKey("traits").ShouldBeFalse();
        message.ContainsKey("correlationId").ShouldBeFalse();
        message.ContainsKey("contentType").ShouldBeFalse();
    }

    [Fact(Skip = "ByteBard 4.0.0-beta.20 throws writing 2.x message traits; re-enable once ByteBardOrg/AsyncAPI.NET#57 and #58 ship.")]
    public async Task TransformAsync_AsyncApi2Document_WithAnnotatedMessage_StillGeneratesDocument()
    {
        // Act
        var document = await GetDocumentAsync(version: AsyncApiVersion.AsyncApi2_0);

        // Assert
        document["asyncapi"]!.GetValue<string>().ShouldBe("2.6.0");
        document["components"]!["messages"]![InvoiceKey]!["externalDocs"]!["url"]!.GetValue<string>()
            .ShouldBe("https://example.com/invoices");
    }

    private static Task<JsonNode> GetDocumentAsync(
        Action<WolverineAsyncApiOptions>? configure = null,
        AsyncApiVersion version = AsyncApiVersion.AsyncApi3_0) =>
        WolverineTestApp.GetDocumentAsync(
            "messaging",
            options =>
            {
                options.AsyncApiVersion = version;
                options.AddServer("kafka", "localhost:9092", "kafka");
                options.AddMessageTrait("traced", new AsyncApiMessageTrait { ContentType = "application/json" });
                options.AddMessageTrait("audited-message", new AsyncApiMessageTrait { Summary = "Audited" });
                options.AddWolverine(wolverine =>
                {
                    wolverine.IncludeSchemes("kafka");
                    configure?.Invoke(wolverine);
                });
            },
            configureWolverine: opts =>
            {
                opts.PublishMessage<InvoiceIssued>().ToKafkaTopic("billing");
                opts.PublishMessage<CreditNoteIssued>().ToKafkaTopic("credit-notes");
            });
}
