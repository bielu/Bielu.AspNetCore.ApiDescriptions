using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bielu.AspNetCore.Arazzo.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Tests;

public partial class ArazzoAnalyzerTests
{
    // BARAZZO009

    private const string ChannelsSource = """

        [Bielu.AspNetCore.AsyncApi.Attributes.AsyncApi]
        internal sealed class OrderEvents
        {
            [Bielu.AspNetCore.AsyncApi.Attributes.Channel("orders")]
            public void Orders() { }

            [Bielu.AspNetCore.AsyncApi.Attributes.Channel("orders/{id}/shipped")]
            public void Shipped() { }
        }

        [Bielu.AspNetCore.AsyncApi.Attributes.Channel("audit")]
        internal sealed class AuditEvents { }
        """;

    private static string ChannelStep(string channel, string extra = "", string source = "events") => $$"""

        new ServiceCollection().AddArazzo(o => o
            .AddAsyncApiSource("events", "v1")
            .AddSourceDescription("partner", "https://partner.example.com/asyncapi.json", ArazzoSourceDescriptionType.AsyncApi)
            .AddWorkflow("w", w => w.Step("s", s => s.Channel("{{source}}", "{{channel}}", ArazzoStepAction.Send{{extra}}))));
        """ + ChannelsSource;

    [Fact]
    public async Task BARAZZO009_UndeclaredChannel_ReportsWarning()
    {
        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep("payments"));

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "BARAZZO009");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        Text(diagnostic).ShouldBe("\"payments\"");
        diagnostic.GetMessage().ShouldBe(
            "No [Channel] declares the channel 'payments' of source 'events'; if a runtime mapping such as Wolverine " +
            "routing or a document transformer creates it, pass dynamic: true");
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("audit")]
    [InlineData("ordersidshipped")]
    public async Task BARAZZO009_DeclaredChannelKey_NoWarning(string channel)
    {
        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep(channel));

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO009_DeclaredNameInsteadOfKey_SuggestsTheKey()
    {
        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep("orders/{id}/shipped"));

        // Assert
        diagnostics.Single(d => d.Id == "BARAZZO009").GetMessage().ShouldEndWith(
            "the document keys the channel declared as 'orders/{id}/shipped' as 'ordersidshipped', so use that");
    }

    [Theory]
    [InlineData(", dynamic: true")]
    [InlineData(", true")]
    [InlineData(", dynamic: System.DateTime.Now.Second > 1")]
    public async Task BARAZZO009_DynamicChannel_NoWarning(string extra)
    {
        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep("payments", extra));

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO009_DynamicFalse_ReportsWarning()
    {
        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep("payments", ", dynamic: false"));

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task BARAZZO009_SourceReachedByUrl_NoWarning()
    {
        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep("payments", source: "partner"));

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO009_WolverineMapsChannels_NoWarning()
    {
        // Arrange - a stand-in for the Wolverine package's extension, which the analyzer matches by name.
        var source = ChannelStep("payments") + """

            namespace Bielu.AspNetCore.AsyncApi.Wolverine
            {
                public static class WolverineAsyncApiOptionsExtensions
                {
                    public static Bielu.AspNetCore.AsyncApi.Services.AsyncApiOptions AddWolverine(
                        this Bielu.AspNetCore.AsyncApi.Services.AsyncApiOptions options) => options;
                }
            }

            internal static class Setup
            {
                public static void Configure(Bielu.AspNetCore.AsyncApi.Services.AsyncApiOptions options) =>
                    Bielu.AspNetCore.AsyncApi.Wolverine.WolverineAsyncApiOptionsExtensions.AddWolverine(options);
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO009_NoChannelAttributes_NoWarning()
    {
        // Arrange
        var source = """

            new ServiceCollection().AddArazzo(o => o
                .AddAsyncApiSource("events", "v1")
                .AddWorkflow("w", w => w.Step("s", s => s.Channel("events", "payments", ArazzoStepAction.Send))));
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO009_ChannelDeclaredInReferencedLibrary_NoWarning()
    {
        // Arrange
        var library = Library("public", "payments");

        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep("payments"), OutputKind.ConsoleApplication, library);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO009_UndeclaredChannelNextToReferencedLibrary_ReportsWarning()
    {
        // Arrange
        var library = Library("public", "payments");

        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep("refunds"), OutputKind.ConsoleApplication, library);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task BARAZZO009_ReferencedLibraryWithOnlyNonPublicChannels_NoWarning()
    {
        // Arrange - metadata doesn't carry non-public members, so the library's channel can't be read and the
        // declared set is incomplete.
        var library = Library("internal", "payments");

        // Act
        var diagnostics = await GetDiagnosticsAsync(ChannelStep("refunds"), OutputKind.ConsoleApplication, library);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO009").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("orders", "orders")]
    [InlineData("orders/{id}", "ordersid")]
    [InlineData("a.b-c_d ef!", "a.b-c_def")]
    public void BARAZZO009_ChannelKey_MatchesTheGenerator(string name, string key)
    {
        RuleBARAZZO009UnknownChannel.ChannelKey(name).ShouldBe(key);
    }

    private static MetadataReference Library(string methodVisibility, string channel)
    {
        var source = $$"""
            namespace Contracts
            {
                internal sealed class Events
                {
                    [Bielu.AspNetCore.AsyncApi.Attributes.Channel("{{channel}}")]
                    {{methodVisibility}} void Send() { }
                }
            }
            """;
        var compilation = CSharpCompilation.Create("Contracts", [CSharpSyntaxTree.ParseText(source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        compilation.Emit(stream).Success.ShouldBeTrue();
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
