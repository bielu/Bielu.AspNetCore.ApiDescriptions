using Bielu.AspNetCore.AsyncApi.Attributes;
using Wolverine.Attributes;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;

/// <summary>
/// Message classes carrying <see cref="MessageAttribute"/>. They have no route of their own: tests that
/// need them documented publish them with <c>configureWolverine</c>, so the shared test app's routes are
/// unchanged.
/// </summary>
[MessageIdentity("billing.invoice-issued")]
[Message(
    Name = "Invoice issued (display)",
    Title = "Invoice issued",
    Summary = "Raised when an invoice is issued.",
    Description = "Attribute description.",
    CorrelationIdLocation = "$message.payload#/invoiceId",
    CorrelationIdDescription = "The invoice id.",
    ExternalDocsUrl = "https://example.com/invoices",
    ExternalDocsDescription = "Invoice docs",
    Traits = ["traced"],
    ContentType = "application/vnd.invoice+json",
    MessageId = "ignored-for-the-key")]
public sealed record InvoiceIssued(Guid InvoiceId);

/// <summary>Carries tags through the constructor form of the attribute.</summary>
[MessageIdentity("billing.credit-note-issued")]
[Message(typeof(CreditNoteIssued), "finance", "billing")]
public sealed record CreditNoteIssued(Guid CreditNoteId);
