using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Tests;

public class ArazzoAnalyzerTests
{
    private const string Usings = @"
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Overlay;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
";

    // Every assembly the test host loads, which includes the ASP.NET Core shared framework and the Arazzo,
    // AsyncAPI and OpenAPI packages through the project references.
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray();

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source,
        OutputKind outputKind = OutputKind.ConsoleApplication)
    {
        var compilation = CSharpCompilation.Create("Test", [CSharpSyntaxTree.ParseText(Usings + source)], References,
            new CSharpCompilationOptions(outputKind));

        // A test whose code doesn't compile against the real API would pass for the wrong reason.
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ArazzoAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static string Text(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    // BARAZZO001

    [Fact]
    public async Task BARAZZO001_AddOpenApiAfterAddArazzo_ReportsWarning()
    {
        // Arrange
        var source = @"
var services = new ServiceCollection();
services.AddArazzo();
services.AddOpenApi();
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "BARAZZO001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("'AddArazzo'");
        Text(diagnostic).ShouldBe("AddOpenApi");
    }

    [Fact]
    public async Task BARAZZO001_AddOpenApiBeforeAddArazzo_NoWarning()
    {
        // Arrange
        var source = @"
var services = new ServiceCollection();
services.AddOpenApi();
services.AddArazzo();
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO001").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO001_AddOpenApiAfterAddAsyncApiAndAddArazzo_ReportsOnce()
    {
        // Arrange - AddAsyncApi is BASYNC010's concern; this rule reports the AddArazzo ordering once.
        var source = @"
var services = new ServiceCollection();
services.AddAsyncApi();
services.AddArazzo();
services.AddOpenApi();
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Count(d => d.Id == "BARAZZO001").ShouldBe(1);
    }

    [Fact]
    public async Task BARAZZO001_AddArazzoFromOtherNamespaceBeforeAddOpenApi_NoWarning()
    {
        // Arrange
        var source = @"
var services = new ServiceCollection();
Some.Other.Library.UnrelatedExtensions.AddArazzo(services);
services.AddOpenApi();

namespace Some.Other.Library
{
    public static class UnrelatedExtensions
    {
        public static IServiceCollection AddArazzo(this IServiceCollection services) => services;
    }
}
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO001").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO001_CallsInDifferentMethods_NoWarning()
    {
        // Arrange - the order across methods isn't knowable statically.
        var source = @"
public class Startup
{
    public void AddWorkflows(IServiceCollection services) => services.AddArazzo();
    public void AddHttp(IServiceCollection services) => services.AddOpenApi();
}
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source, OutputKind.DynamicallyLinkedLibrary);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO001").ShouldBeEmpty();
    }

    // BARAZZO002

    [Theory]
    [InlineData(@"o.AddWorkflow(""bad id"", w => w.Step(""a"", s => s.Operation(""op"")));", "bad id")]
    [InlineData(@"o.AddSourceDescription(""bad.name"", ""https://example.com/api.json"", ""openapi"");", "bad.name")]
    [InlineData(@"o.AddAsyncApiSource(""bad/name"", ""v1"");", "bad/name")]
    [InlineData(@"o.AddWorkflow(""w"", w => w.Step(""step 1"", s => s.Operation(""op"")));", "step 1")]
    [InlineData(@"o.AddWorkflow(""w"", w => w.DependsOn(""ok"", ""not ok"").Step(""a"", s => s.Operation(""op"")));", "not ok")]
    [InlineData(@"o.AddWorkflow(""w"", w => w.Step(""a"", s => s.Channel(""src!"", ""orders"", ""send"")));", "src!")]
    [InlineData(@"o.AddWorkflow(""w"", w => w.Step(""a"", s => s.Workflow(""other workflow"")));", "other workflow")]
    public async Task BARAZZO002_InvalidLiteralId_ReportsError(string call, string invalidId)
    {
        // Arrange
        var source = $@"
new ServiceCollection().AddArazzo(o => {{ {call} }});
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "BARAZZO002");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain($"'{invalidId}'");
        Text(diagnostic).ShouldBe($"\"{invalidId}\"");
    }

    [Fact]
    public async Task BARAZZO002_GenericMarkerType_ReportsError()
    {
        // Arrange - a generic type's name carries a backtick and arity, which ArazzoId.FromType rejects at runtime.
        var source = @"
new ServiceCollection().AddArazzo(o => o.AddWorkflow<Marker<int>>(w => w.Step(""a"", s => s.Operation(""op""))));

public class Marker<T>;
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "BARAZZO002");
        diagnostic.GetMessage().ShouldContain("'marker`1'");
        Text(diagnostic).ShouldBe("AddWorkflow<Marker<int>>");
    }

    [Fact]
    public async Task BARAZZO002_ValidIdsAndFreeFormNames_NoError()
    {
        // Arrange - Output and Parameter names aren't identifiers, and WithInputs<T> isn't a marker type.
        var source = @"
new ServiceCollection().AddArazzo(o => o
    .AddAsyncApiSource(""events_v-1"", ""v1"")
    .AddWorkflow<PlaceOrder>(w => w
        .WithInputs<System.Collections.Generic.List<int>>()
        .Step(""send-order"", s => s
            .Channel(""events_v-1"", ""orders/{id}"", ArazzoStepAction.Send)
            .Output(""order id"", ""$message.payload#/id"")
            .Parameter(""X-Trace.Id"", ""$inputs.trace""))));

public class PlaceOrder;
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO002").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("MeasureAndAlert")]
    [InlineData("URLValue")]
    [InlineData("IOStream")]
    [InlineData("A")]
    [InlineData("already")]
    [InlineData("ABC")]
    [InlineData("Marker`1")]
    public void MarkerTypeId_MatchesSystemTextJsonCamelCase(string typeName)
    {
        // Act
        var id = ArazzoSymbols.ToCamelCase(typeName);

        // Assert - ArazzoId.FromType uses JsonNamingPolicy.CamelCase; the analyzer's copy must agree.
        id.ShouldBe(JsonNamingPolicy.CamelCase.ConvertName(typeName));
    }

    // BARAZZO003

    [Fact]
    public async Task BARAZZO003_StepWithoutTarget_ReportsError()
    {
        // Arrange
        var source = @"
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w.Step(""lonely"", s => s.WithDescription(""no target""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "BARAZZO003");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'lonely' sets no target");
        Text(diagnostic).ShouldBe("Step");
    }

    [Fact]
    public async Task BARAZZO003_StepWithTwoTargets_ReportsErrorOnSecond()
    {
        // Arrange
        var source = @"
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w.Step(""a"", s => s.Operation(""op"").Workflow(""other""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "BARAZZO003");
        diagnostic.GetMessage().ShouldContain("'Workflow' after 'Operation'");
        Text(diagnostic).ShouldBe("Workflow");
    }

    [Fact]
    public async Task BARAZZO003_StepWithOneTarget_NoError()
    {
        // Arrange
        var source = @"
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w
    .Step(""a"", s => s.Operation(""op""))
    .Step(""b"", s => { s.WithDescription(""block body""); s.OperationPath(""api"", ""/orders/{id}"", ""GET""); })));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO003").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO003_TargetsInBranches_NoError()
    {
        // Arrange - only one branch runs, so neither 'none' nor 'two' can be claimed.
        var source = @"
var useChannel = args.Length > 0;
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w.Step(""a"", s =>
{
    if (useChannel) { s.Channel(""events"", ""orders"", ""send""); }
    else { s.Operation(""op""); }
})));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO003").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO003_BuilderPassedToHelper_NoError()
    {
        // Arrange - the helper may set the target.
        var source = @"
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w.Step(""a"", s => Helpers.Target(s))));

static class Helpers
{
    public static void Target(ArazzoStepBuilder step) => step.Operation(""op"");
}
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO003").ShouldBeEmpty();
    }

    // BARAZZO004

    [Theory]
    [InlineData("publish")]
    [InlineData("Send")]
    public async Task BARAZZO004_UnknownAction_ReportsError(string action)
    {
        // Arrange
        var source = $@"
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w.Step(""a"", s => s.Channel(""events"", ""orders"", ""{action}""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "BARAZZO004");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain($"'{action}'");
        Text(diagnostic).ShouldBe($"\"{action}\"");
    }

    [Fact]
    public async Task BARAZZO004_KnownOrNonConstantAction_NoError()
    {
        // Arrange
        var source = @"
var action = args[0];
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w
    .Step(""a"", s => s.Channel(""events"", ""orders"", ArazzoStepAction.Send))
    .Step(""b"", s => s.Channel(""events"", ""orders"", ""receive""))
    .Step(""c"", s => s.Channel(""events"", ""orders"", action))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO004").ShouldBeEmpty();
    }

    // BARAZZO005

    [Theory]
    [InlineData(@"s.Channel(""events"", ""orders"", ArazzoStepAction.Send).WithCorrelationId(""$message.header#/id"")")]
    [InlineData(@"s.Operation(""getOrder"").WithCorrelationId(""$message.header#/id"")")]
    public async Task BARAZZO005_CorrelationIdWithoutReceive_ReportsWarning(string step)
    {
        // Arrange
        var source = $@"
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w.Step(""a"", s => {step})));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "BARAZZO005");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        Text(diagnostic).ShouldBe("WithCorrelationId");
    }

    [Fact]
    public async Task BARAZZO005_CorrelationIdOnReceiveOrUnknownAction_NoWarning()
    {
        // Arrange
        var source = @"
var action = args[0];
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w => w
    .Step(""a"", s => s.Channel(""events"", ""orders"", ArazzoStepAction.Receive).WithCorrelationId(""$message.header#/id""))
    .Step(""b"", s => s.Channel(""events"", ""orders"", action).WithCorrelationId(""$message.header#/id""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO005").ShouldBeEmpty();
    }

    // BARAZZO006

    [Fact]
    public async Task BARAZZO006_UndeclaredSource_ReportsWarning()
    {
        // Arrange
        var source = @"
new ServiceCollection().AddArazzo(""orders"", o => o
    .AddAsyncApiSource(""events"", ""v1"")
    .AddWorkflow(""w"", w => w
        .Step(""a"", s => s.Channel(""event"", ""orders"", ""send""))
        .Step(""b"", s => s.OperationPath(""http"", ""/orders"", ""POST""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var reported = diagnostics.Where(d => d.Id == "BARAZZO006").Select(Text).ToList();
        reported.ShouldBe(["\"event\"", "\"http\""], ignoreOrder: true);
        diagnostics.First(d => d.Id == "BARAZZO006").GetMessage().ShouldContain("Arazzo document 'orders'");
    }

    [Fact]
    public async Task BARAZZO006_SourceDeclaredInAnotherAddArazzoCallForSameDocument_NoWarning()
    {
        // Arrange - both calls configure the options of document 'v1' (the default name).
        var source = @"
var services = new ServiceCollection();
services.AddArazzo(""V1"", o => o.AddOpenApiSource(""http"", ""v1"").AddSourceDescription(""external"", ""https://example.com/a.json"", ""openapi""));
services.AddArazzo(o => o.AddWorkflow(""w"", w => w
    .Step(""a"", s => s.OperationPath(""http"", ""/orders"", ""POST""))
    .Step(""b"", s => s.OperationPath(""external"", ""/x"", ""GET""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO006").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO006_SourceInAnotherDocument_ReportsWarning()
    {
        // Arrange
        var source = @"
var services = new ServiceCollection();
services.AddArazzo(""a"", o => o.AddOpenApiSource(""http"", ""v1""));
services.AddArazzo(""b"", o => o.AddWorkflow(""w"", w => w.Step(""s"", s => s.OperationPath(""http"", ""/orders"", ""POST""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Single(d => d.Id == "BARAZZO006").GetMessage().ShouldContain("document 'b'");
    }

    [Theory]
    [InlineData(@"services.Configure<ArazzoOptions>(""v1"", o => o.AddOpenApiSource(""http"", ""v1""));")]
    [InlineData(@"services.AddArazzo(Helpers.Configure);")]
    [InlineData(@"services.AddArazzo(o => Helpers.Configure(o));")]
    [InlineData(@"services.AddArazzo(args[0], o => o.AddOpenApiSource(""http"", ""v1""));")]
    [InlineData(@"services.AddArazzo(o => o.AddOpenApiSource(args[0], ""v1""));")]
    public async Task BARAZZO006_DeclarationsNotVisible_NoWarning(string otherRegistration)
    {
        // Arrange
        var source = $@"
var services = new ServiceCollection();
{otherRegistration}
services.AddArazzo(o => o.AddWorkflow(""w"", w => w.Step(""s"", s => s.OperationPath(""http"", ""/orders"", ""POST""))));

static class Helpers
{{
    public static void Configure(ArazzoOptions options) => options.AddOpenApiSource(""http"", ""v1"");
}}
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO006").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO006_OverlayExtensionOnOptions_StillChecked()
    {
        // Arrange - the package's own AddOverlay doesn't declare sources, so the document stays checkable.
        var source = @"
new ServiceCollection().AddArazzo(o => o
    .AddOverlay(""overlays/public.yaml"")
    .AddWorkflow(""w"", w => w.Step(""s"", s => s.OperationPath(""http"", ""/orders"", ""POST""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Count(d => d.Id == "BARAZZO006").ShouldBe(1);
    }

    // BARAZZO007

    [Fact]
    public async Task BARAZZO007_UnknownWorkflowAndStep_ReportsWarnings()
    {
        // Arrange
        var source = @"
new ServiceCollection().AddArazzo(o => o
    .AddWorkflow<PlaceOrder>(w => w.Step(""create"", s => s.Operation(""createOrder"")))
    .AddWorkflow(""ship"", w => w
        .DependsOn<PlaceOrder>()
        .DependsOn(""placeOrders"")
        .Step(""pack"", s => s.Operation(""pack""))
        .Step(""send"", s => s.Workflow(""notify"").DependsOn(""pack"", ""packk""))));

public class PlaceOrder;
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var reported = diagnostics.Where(d => d.Id == "BARAZZO007").ToList();
        reported.Select(Text).ShouldBe(["\"placeOrders\"", "\"notify\"", "\"packk\""], ignoreOrder: true);
        reported.Single(d => Text(d) == "\"packk\"").GetMessage().ShouldContain("step in this workflow");
        reported.ShouldAllBe(d => d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task BARAZZO007_StepDependsOnStepOfAnotherWorkflow_ReportsWarning()
    {
        // Arrange - a step's DependsOn names steps of its own workflow.
        var source = @"
new ServiceCollection().AddArazzo(o => o
    .AddWorkflow(""a"", w => w.Step(""first"", s => s.Operation(""op"")))
    .AddWorkflow(""b"", w => w.Step(""second"", s => s.Operation(""op"").DependsOn(""first""))));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        Text(diagnostics.Single(d => d.Id == "BARAZZO007")).ShouldBe("\"first\"");
    }

    [Fact]
    public async Task BARAZZO007_StepsAddedOutsideLambda_NoWarning()
    {
        // Arrange - the workflow builder goes to a helper, so its step ids aren't all visible.
        var source = @"
new ServiceCollection().AddArazzo(o => o.AddWorkflow(""w"", w =>
{
    Helpers.AddSteps(w);
    w.Step(""last"", s => s.Operation(""op"").DependsOn(""fromHelper""));
}));

static class Helpers
{
    public static void AddSteps(ArazzoWorkflowBuilder workflow) => workflow.Step(""fromHelper"", s => s.Operation(""op""));
}
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO007").ShouldBeEmpty();
    }

    [Fact]
    public async Task BARAZZO007_MarkerTypeReferences_MatchStringIds()
    {
        // Arrange - Step<CreateOrder> and DependsOn(""createOrder"") name the same step.
        var source = @"
new ServiceCollection().AddArazzo(o => o
    .AddWorkflow(""placeOrder"", w => w
        .Step<CreateOrder>(s => s.Operation(""create""))
        .Step(""confirm"", s => s.Operation(""confirm"").DependsOn(""createOrder"").DependsOn<CreateOrder>()))
    .AddWorkflow(""ship"", w => w.Step(""s"", s => s.Workflow<PlaceOrder>())));

public class CreateOrder;
public class PlaceOrder;
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO007").ShouldBeEmpty();
    }

    // BARAZZO008

    [Fact]
    public async Task BARAZZO008_SourceDocumentNotRegistered_ReportsInfo()
    {
        // Arrange
        var source = @"
var services = new ServiceCollection();
services.AddOpenApi(""public"");
services.AddAsyncApi(""events"", _ => { });
services.AddArazzo(o => o
    .AddAsyncApiSource(""a"", ""Events"")
    .AddAsyncApiSource(""b"", ""v1"")
    .AddOpenApiSource(""c"", ""internal""));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var reported = diagnostics.Where(d => d.Id == "BARAZZO008").ToList();
        reported.Select(Text).ShouldBe(["\"v1\"", "\"internal\""], ignoreOrder: true);
        reported.ShouldAllBe(d => d.Severity == DiagnosticSeverity.Info);
        reported.Single(d => Text(d) == "\"v1\"").GetMessage()
            .ShouldBe("'AddAsyncApiSource' points at the AsyncAPI document 'v1', which this project doesn't register with 'AddAsyncApi'");
    }

    [Fact]
    public async Task BARAZZO008_DefaultDocumentNames_NoInfo()
    {
        // Arrange - AddAsyncApi() and AddOpenApi() both register 'v1'.
        var source = @"
var services = new ServiceCollection();
services.AddOpenApi();
services.AddAsyncApi();
services.AddArazzo(o => o.AddAsyncApiSource(""a"", ""v1"").AddOpenApiSource(""b"", ""V1""));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO008").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"services.AddAsyncApi(args[0]);")]
    public async Task BARAZZO008_RegistrationsNotVisible_NoInfo(string registration)
    {
        // Arrange - no registration in this project, or one whose name isn't a constant.
        var source = $@"
var services = new ServiceCollection();
{registration}
services.AddArazzo(o => o.AddAsyncApiSource(""a"", ""events""));
";

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        diagnostics.Where(d => d.Id == "BARAZZO008").ShouldBeEmpty();
    }
}
