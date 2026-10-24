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

        [AsyncApi("__DOC__")]
        public sealed class Channels
        {
            [Channel("invoices/issued")]
            [Message(typeof(InvoiceIssued))]
            [SubscribeOperation(OperationId = "invoiceIssuedOperation")]
            public void Issued() { }

            [Channel("invoices/paid")]
            [Message(typeof(InvoicePaid), Title = "Overridden title", ContentType = "text/plain")]
            [SubscribeOperation(OperationId = "invoicePaidOperation")]
            public void Paid() { }

            [Channel("invoices/voided")]
            [SubscribeOperation(typeof(InvoiceVoided), "invoices", OperationId = "invoiceVoidedOperation")]
            public void Voided() { }

            [Channel("invoices/disputed")]
            [Message(typeof(InvoiceDisputed), MessageId = "invoiceDisputed")]
            [SubscribeOperation(OperationId = "invoiceDisputedOperation")]
            public void Disputed() { }
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

        return new Compiled(assembly, provider);
    }

    private sealed record Compiled(Assembly Assembly, IAsyncApiMetadataProvider Provider);

    private sealed class OverridingProvider(
        IAsyncApiMetadataProvider inner,
        IReadOnlyDictionary<Type, MessageAttribute> classAttributes) : IAsyncApiMetadataProvider
    {
        public IEnumerable<AsyncApiTypeMetadata> GetMetadata(string documentName) => inner.GetMetadata(documentName);

        public IReadOnlyDictionary<Type, MessageAttribute>? GetPayloadMessageAttributes() => classAttributes;
    }
}
