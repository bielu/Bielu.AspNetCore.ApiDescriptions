using Bielu.AspNetCore.AsyncApi.Attributes;
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
/// A <see cref="MessageAttribute"/> on a payload class supplies defaults wherever the class is used as a payload,
/// and a <see cref="MessageAttribute"/> on a method overrides them.
/// </summary>
public class MessageAttributeOnClassTests
{
    internal const string DocumentName = "message-attribute-on-class";

    [Message(MessageId = "invoiceIssued", Title = "Invoice issued", Summary = "An invoice was issued.",
        CorrelationIdLocation = "$message.header#/correlationId", ExternalDocsUrl = "https://example.com/invoices",
        Traits = ["traced"])]
    public sealed record InvoiceIssued(string InvoiceId);

    [Message(MessageId = "invoicePaid", Title = "Paid by default", Summary = "An invoice was paid.")]
    public sealed record InvoicePaid(string InvoiceId);

    [Message(CorrelationIdLocation = "$message.header#/voidCorrelation")]
    public sealed record InvoiceVoided(string InvoiceId);

    public sealed record InvoiceDisputed(string InvoiceId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("invoices/issued")]
        [Message(typeof(InvoiceIssued))]
        [SendOperation(OperationId = "invoiceIssuedOperation")]
        public void Issued()
        {
        }

        [Channel("invoices/paid")]
        [Message(typeof(InvoicePaid), Title = "Overridden title")]
        [SendOperation(OperationId = "invoicePaidOperation")]
        public void Paid()
        {
        }

        [Channel("invoices/voided")]
        [SendOperation(typeof(InvoiceVoided), "invoices", OperationId = "invoiceVoidedOperation")]
        public void Voided()
        {
        }

        [Channel("invoices/disputed")]
        [Message(typeof(InvoiceDisputed), MessageId = "invoiceDisputed")]
        [SendOperation(OperationId = "invoiceDisputedOperation")]
        public void Disputed()
        {
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_PayloadClassWithMessageAttribute_AppliesItsValues()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var message = document.Components.Messages["invoiceIssued"];
        message.Title.ShouldBe("Invoice issued");
        message.Summary.ShouldBe("An invoice was issued.");
        message.CorrelationId.ShouldNotBeNull().Location.ShouldBe("$message.header#/correlationId");
        message.ExternalDocs.ShouldNotBeNull().Url.ToString().ShouldBe("https://example.com/invoices");
        message.Traits.ShouldNotBeNull().Single().ShouldBeOfType<AsyncApiMessageTraitReference>().Reference.Reference.ShouldEndWith("traced");
    }

    [Fact]
    public async Task GetAsyncApiDocument_MethodAttributeSetsValue_OverridesPayloadClass()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var message = document.Components.Messages["invoicePaid"];
        message.Title.ShouldBe("Overridden title");
        message.Summary.ShouldBe("An invoice was paid.");
    }

    [Fact]
    public async Task GetAsyncApiDocument_PayloadClassUsedThroughOperationPayloadType_AppliesItsValues()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Components.Messages.Values
            .Single(m => m.CorrelationId?.Location == "$message.header#/voidCorrelation")
            .Payload.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAsyncApiDocument_PayloadClassWithoutMessageAttribute_KeepsMethodValues()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var message = document.Components.Messages["invoiceDisputed"];
        message.CorrelationId.ShouldBeNull();
        message.ExternalDocs.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Parameterless_LeavesPayloadTypeEmpty()
    {
        // Act
        var attribute = new MessageAttribute();

        // Assert
        attribute.PayloadType.ShouldBeNull();
        attribute.Tags.ShouldBeEmpty();
    }

    private static async Task<AsyncApiDocument> GetDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName);

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
