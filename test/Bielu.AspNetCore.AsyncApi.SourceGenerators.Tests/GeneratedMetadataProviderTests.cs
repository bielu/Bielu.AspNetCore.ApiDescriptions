using System.Reflection;
using System.Runtime.Loader;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Models.Metadata;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.SourceGenerators;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.SourceGenerators.Tests;

/// <summary>
/// Runs <see cref="AsyncApiMetadataGenerator"/> over a sample, loads the result and checks that the generated provider
/// describes the same document as the reflection-based one, including [Message] placed on payload classes.
/// </summary>
public class GeneratedMetadataProviderTests
{
    private const string DocumentToken = "__DOC__";

    private const string Source = """
        using Bielu.AspNetCore.AsyncApi.Attributes;

        namespace Sample;

        public sealed record InvoiceHeaders(string Tenant);

        [Message(MessageId = "invoiceIssued", Title = "Invoice issued", Summary = "An invoice was issued.",
            ContentType = "application/vnd.invoice+json", CorrelationIdLocation = "$message.header#/correlationId",
            CorrelationIdDescription = "Correlates the \"issued\" event", ExternalDocsUrl = "https://example.com/invoices",
            ExternalDocsDescription = "Invoice docs", HeadersType = typeof(InvoiceHeaders), Traits = new[] { "traced" })]
        public sealed record InvoiceIssued(string InvoiceId);

        [Message(MessageId = "invoicePaid", Title = "Paid by default", Summary = "An invoice was paid.", ContentType = "application/json")]
        public sealed record InvoicePaid(string InvoiceId);

        [Message(CorrelationIdLocation = "$message.header#/voidCorrelation")]
        public sealed record InvoiceVoided(string InvoiceId);

        public sealed record InvoiceDisputed(string InvoiceId);

        public sealed record InvoiceRequested(string InvoiceId);

        public sealed record InvoiceArchived(string InvoiceId);

        public enum ArchiveReason { Expired = -1, Requested = 1 }

        // A custom operation attribute: it sets the payload in its constructor and adds a property of its own.
        public sealed class ArchivesInvoiceAttribute : SendOperationAttribute
        {
            public ArchivesInvoiceAttribute(ArchiveReason reason, double retentionYears, params string[] tags)
                : base(typeof(InvoiceArchived), tags)
            {
                Description = $"Archived ({reason}) after {retentionYears} years.";
            }

            public string? Archive { get => Summary; set => Summary = $"Moved to {value}."; }
        }

        [AsyncApi("__DOC__")]
        public sealed class Channels
        {
            [Channel("invoices/issued")]
            [Message(typeof(InvoiceIssued))]
            [SendOperation(OperationId = "invoiceIssuedOperation")]
            public void Issued() { }

            [Channel("invoices/paid")]
            [Message(typeof(InvoicePaid), Title = "Overridden title", ContentType = "text/plain")]
            [SendOperation(OperationId = "invoicePaidOperation")]
            public void Paid() { }

            [Channel("invoices/voided")]
            [SendOperation(typeof(InvoiceVoided), "invoices", OperationId = "invoiceVoidedOperation")]
            public void Voided() { }

            [Channel("invoices/disputed")]
            [Message(typeof(InvoiceDisputed), MessageId = "invoiceDisputed")]
            [SendOperation(OperationId = "invoiceDisputedOperation")]
            public void Disputed() { }

            [Channel("invoices/requested")]
            [ReceiveOperation(typeof(InvoiceRequested), OperationId = "invoiceRequestedOperation")]
            public void Requested() { }

            [Channel("invoices/legacy-requested")]
            [PublishOperation(typeof(InvoiceRequested), OperationId = "invoiceLegacyRequestedOperation")]
            public void LegacyRequested() { }

            [Channel("invoices/archived")]
            [ArchivesInvoice(ArchiveReason.Expired, 1.5, "invoices", OperationId = "invoiceArchivedOperation", Archive = "cold storage")]
            public void Archived() { }
        }
        """;

    // The custom operation attribute is private, so the generated provider cannot name it.
    private const string InaccessibleOperationSource = """
        using Bielu.AspNetCore.AsyncApi.Attributes;

        namespace Sample;

        public sealed record InvoiceArchived(string InvoiceId);

        [AsyncApi("__DOC__")]
        public sealed class Channels
        {
            [Channel("invoices/archived")]
            [ArchivesInvoice]
            public void Archived() { }

            private sealed class ArchivesInvoiceAttribute : SendOperationAttribute
            {
                public ArchivesInvoiceAttribute() : base(typeof(InvoiceArchived)) { }
            }
        }
        """;

    // The payload classes carry [Message], but no class is annotated with [AsyncApi].
    private const string MessageClassOnlySource = """
        using Bielu.AspNetCore.AsyncApi.Attributes;

        namespace Sample;

        [Message(Title = "Standalone")]
        public sealed class Standalone { }

        [Message(Title = "Generic")]
        public sealed class Generic<T> { }
        """;

    [Fact]
    public void GetPayloadMessageAttributes_PayloadClassWithMessageAttribute_CopiesEveryProperty()
    {
        // Arrange
        var compiled = Compile(Source, NewDocumentName());

        // Act
        var attributes = compiled.Provider.GetPayloadMessageAttributes().ShouldNotBeNull();

        // Assert
        var issued = attributes[compiled.Assembly.GetType("Sample.InvoiceIssued")!];
        issued.PayloadType.ShouldBeNull();
        issued.MessageId.ShouldBe("invoiceIssued");
        issued.Title.ShouldBe("Invoice issued");
        issued.Summary.ShouldBe("An invoice was issued.");
        issued.ContentType.ShouldBe("application/vnd.invoice+json");
        issued.CorrelationIdLocation.ShouldBe("$message.header#/correlationId");
        issued.CorrelationIdDescription.ShouldBe("Correlates the \"issued\" event");
        issued.ExternalDocsUrl.ShouldBe("https://example.com/invoices");
        issued.ExternalDocsDescription.ShouldBe("Invoice docs");
        issued.HeadersType.ShouldBe(compiled.Assembly.GetType("Sample.InvoiceHeaders"));
        issued.Traits.ShouldBe(["traced"]);
    }

    [Fact]
    public void GetPayloadMessageAttributes_PayloadClassWithoutMessageAttribute_IsNotListed()
    {
        // Arrange
        var compiled = Compile(Source, NewDocumentName());

        // Act
        var attributes = compiled.Provider.GetPayloadMessageAttributes().ShouldNotBeNull();

        // Assert
        attributes.ContainsKey(compiled.Assembly.GetType("Sample.InvoiceDisputed")!).ShouldBeFalse();
        attributes.ContainsKey(compiled.Assembly.GetType("Sample.Channels")!).ShouldBeFalse();
    }

    [Fact]
    public void GetPayloadMessageAttributes_MessageClassWithoutAsyncApiClass_StillGeneratesAndSkipsOpenGenerics()
    {
        // Arrange
        var compiled = Compile(MessageClassOnlySource, NewDocumentName());

        // Act
        var attributes = compiled.Provider.GetPayloadMessageAttributes().ShouldNotBeNull();
        var metadata = compiled.Provider.GetMetadata("anything").ToList();

        // Assert
        attributes.Keys.Select(k => k.Name).ShouldBe(["Standalone"]);
        attributes.Values.Single().Title.ShouldBe("Standalone");
        metadata.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsyncApiDocument_GeneratedProvider_MatchesReflectionProvider()
    {
        // Arrange
        var documentName = NewDocumentName();
        var compiled = Compile(Source, documentName);

        // Act
        var reflectionJson = await GetDocumentJsonAsync(documentName, provider: null);
        var generatedJson = await GetDocumentJsonAsync(documentName, compiled.Provider);

        // Assert
        generatedJson.ShouldBe(reflectionJson);
        generatedJson.ShouldContain("Invoice issued");
    }

    [Theory]
    [InlineData("invoiceIssuedOperation", AsyncApiAction.Send)]
    [InlineData("invoiceRequestedOperation", AsyncApiAction.Receive)]
    [InlineData("invoiceLegacyRequestedOperation", AsyncApiAction.Receive)]
    [InlineData("invoiceArchivedOperation", AsyncApiAction.Send)]
    public async Task GetAsyncApiDocument_GeneratedProvider_SetsOperationAction(string operationId, AsyncApiAction expected)
    {
        // Arrange
        var documentName = NewDocumentName();
        var compiled = Compile(Source, documentName);

        // Act
        var document = await GetDocumentAsync(documentName, compiled.Provider);

        // Assert
        document.Operations[operationId].Action.ShouldBe(expected);
    }

    [Fact]
    public async Task GetAsyncApiDocument_DerivedOperationAttribute_RebuildsItWithItsOwnArguments()
    {
        // Arrange
        var documentName = NewDocumentName();
        var compiled = Compile(Source, documentName);

        // Act
        var document = await GetDocumentAsync(documentName, compiled.Provider);

        // Assert
        var archived = document.Operations["invoiceArchivedOperation"];
        archived.Description.ShouldBe($"Archived (Expired) after {1.5} years.");
        archived.Summary.ShouldBe("Moved to cold storage.");
        archived.Tags.ShouldHaveSingleItem().Name.ShouldBe("invoices");
        archived.Messages.ShouldHaveSingleItem();
        compiled.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsyncApiDocument_InaccessibleOperationAttribute_ReportsDiagnosticAndSkipsOperation()
    {
        // Arrange
        var documentName = NewDocumentName();
        var compiled = Compile(InaccessibleOperationSource, documentName);

        // Act
        var document = await GetDocumentAsync(documentName, compiled.Provider);

        // Assert
        var diagnostic = compiled.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("BASYNC011");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("Sample.Channels.ArchivesInvoiceAttribute");
        diagnostic.Location.IsInSource.ShouldBeTrue();
        document.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsyncApiDocument_GeneratedProvider_AppliesClassLevelMessageMethodOverrideAndContentType()
    {
        // Arrange
        var documentName = NewDocumentName();
        var compiled = Compile(Source, documentName);

        // Act
        var document = await GetDocumentAsync(documentName, compiled.Provider);

        // Assert
        var issued = document.Components.Messages["invoiceIssued"];
        issued.Title.ShouldBe("Invoice issued");
        issued.ContentType.ShouldBe("application/vnd.invoice+json");
        issued.CorrelationId.ShouldNotBeNull().Location.ShouldBe("$message.header#/correlationId");
        issued.ExternalDocs.ShouldNotBeNull().Url.ToString().ShouldBe("https://example.com/invoices");
        issued.Headers.ShouldNotBeNull();

        var paid = document.Components.Messages["invoicePaid"];
        paid.Title.ShouldBe("Overridden title");
        paid.ContentType.ShouldBe("text/plain");
        paid.Summary.ShouldBe("An invoice was paid.");

        document.Components.Messages.Values
            .Single(m => m.CorrelationId?.Location == "$message.header#/voidCorrelation")
            .Payload.ShouldNotBeNull();
        document.Components.Messages["invoiceDisputed"].CorrelationId.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsyncApiDocument_ProviderSuppliesClassMetadata_UsesItInsteadOfReflection()
    {
        // Arrange
        var documentName = NewDocumentName();
        var compiled = Compile(Source, documentName);
        var disputed = compiled.Assembly.GetType("Sample.InvoiceDisputed")!;
        var provider = new OverridingProvider(compiled.Provider, new Dictionary<Type, MessageAttribute>
        {
            [disputed] = new() { CorrelationIdLocation = "$message.header#/supplied" },
        });

        // Act
        var document = await GetDocumentAsync(documentName, provider);

        // Assert
        document.Components.Messages["invoiceDisputed"].CorrelationId.ShouldNotBeNull().Location.ShouldBe("$message.header#/supplied");
    }

    [Fact]
    public async Task GetAsyncApiDocument_ProviderSuppliesEmptyClassMetadata_IgnoresClassAttributes()
    {
        // Arrange
        var documentName = NewDocumentName();
        var compiled = Compile(Source, documentName);
        var provider = new OverridingProvider(compiled.Provider, new Dictionary<Type, MessageAttribute>());

        // Act
        var document = await GetDocumentAsync(documentName, provider);

        // Assert
        document.Components.Messages["invoiceIssued"].ContentType.ShouldNotBe("application/vnd.invoice+json");
    }

    private static string NewDocumentName() => "gen" + Guid.NewGuid().ToString("N");

    private static async Task<AsyncApiDocument> GetDocumentAsync(string documentName, IAsyncApiMetadataProvider? provider)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(documentName);
        if (provider is not null)
        {
            builder.Services.Replace(ServiceDescriptor.KeyedSingleton(documentName, provider));
        }

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(documentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }

    private static async Task<string> GetDocumentJsonAsync(string documentName, IAsyncApiMetadataProvider? provider) =>
        (await GetDocumentAsync(documentName, provider)).SerializeAsJson(AsyncApiVersion.AsyncApi3_0);

    private static Compiled Compile(string source, string documentName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source.Replace(DocumentToken, documentName));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "GeneratedSample" + Guid.NewGuid().ToString("N"),
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AsyncApiMetadataGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        using var stream = new MemoryStream();
        var emitResult = outputCompilation.Emit(stream);
        emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString()).ShouldBeEmpty();

        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        var providerType = assembly.GetType("Bielu.AspNetCore.AsyncApi.Generated.GeneratedAsyncApiMetadataProvider")!;
        var provider = (IAsyncApiMetadataProvider)Activator.CreateInstance(providerType)!;

        return new Compiled(assembly, provider, generatorDiagnostics);
    }

    private sealed record Compiled(Assembly Assembly, IAsyncApiMetadataProvider Provider, IReadOnlyList<Diagnostic> Diagnostics);

    private sealed class OverridingProvider(
        IAsyncApiMetadataProvider inner,
        IReadOnlyDictionary<Type, MessageAttribute> classAttributes) : IAsyncApiMetadataProvider
    {
        public IEnumerable<AsyncApiTypeMetadata> GetMetadata(string documentName) => inner.GetMetadata(documentName);

        public IReadOnlyDictionary<Type, MessageAttribute>? GetPayloadMessageAttributes() => classAttributes;
    }
}
