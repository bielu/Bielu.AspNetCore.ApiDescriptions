using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Tests;

public class AsyncApiAttributeAnalyzerTests
{
    private async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Bielu.AspNetCore.AsyncApi.Attributes.AsyncApiAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName("System.Runtime")).Location),
            MetadataReference.CreateFromFile(System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName("netstandard")).Location),
        };

        var compilation = CSharpCompilation.Create("Test", new[] { syntaxTree }, references, new CSharpCompilationOptions(outputKind));
        var analyzer = new AsyncApiAttributeAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task BASYNC001_MissingAsyncApiAttribute_OnType_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;

[Channel(""test"")]
public class TestChannel
{
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.ShouldContain(d => d.Id == "BASYNC001" && d.GetMessage().Contains("TestChannel"));
    }

    [Fact]
    public async Task BASYNC001_WithAsyncApiAttribute_NoWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;

[AsyncApi]
[Channel(""test"")]
public class TestChannel
{
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.Where(d => d.Id == "BASYNC001").ShouldBeEmpty();
    }

    [Fact]
    public async Task BASYNC002_MissingChannelAttribute_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;

[AsyncApi]
public class TestHub
{
    [PublishOperation]
    public void SendMessage() {}
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.ShouldContain(d => d.Id == "BASYNC002" && d.GetMessage().Contains("SendMessage"));
    }

    [Fact]
    public async Task BASYNC003_DuplicateName_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;

[AsyncApi]
public class TestHub
{
    [Channel(""test"")]
    [Message(typeof(string), Name = ""msg"")]
    [Message(typeof(int), Name = ""msg"")]
    public void SendMessage() {}
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.ShouldContain(d => d.Id == "BASYNC003" && d.GetMessage().Contains("msg"));
    }

    [Fact]
    public async Task BASYNC005_InvalidPayloadType_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;
using System;

[AsyncApi]
public class TestHub
{
    [Channel(""test"")]
    [Message(typeof(void))]
    public void SendMessage() {}
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.ShouldContain(d => d.Id == "BASYNC005" && d.GetMessage().Contains("void"));
    }

    [Fact]
    public async Task BASYNC004_UnusedDocumentName_ReportsInfo()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;

[AsyncApi(""unused"")]
public class TestHub
{
}

public class Startup
{
    public void ConfigureServices(object services)
    {
        AsyncApiServiceCollectionExtensions.AddAsyncApi(services, ""used"");
    }
}

namespace Bielu.AspNetCore.AsyncApi.Extensions
{
    public static class AsyncApiServiceCollectionExtensions
    {
        public static void AddAsyncApi(object services, string documentName) {}
    }
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.ShouldContain(d => d.Id == "BASYNC004" && d.GetMessage().Contains("unused"));
    }

    [Fact]
    public async Task BASYNC006_InvalidJsonExample_ReportsError()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;

[AsyncApi]
public class TestHub
{
    [Channel(""test"")]
    [MessageExample(Json = ""{ invalid json"")]
    public void SendMessage() {}
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.ShouldContain(d => d.Id == "BASYNC006");
    }

    [Fact]
    public async Task BASYNC007_ProviderMissingConstructor_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;
using System;

public class MyProvider : IAsyncApiMessageExampleProvider
{
    public MyProvider(string arg) {}
}

[AsyncApi]
public class TestHub
{
    [Channel(""test"")]
    [MessageExample(ProviderType = typeof(MyProvider))]
    public void SendMessage() {}
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.ShouldContain(d => d.Id == "BASYNC007" && d.GetMessage().Contains("MyProvider"));
    }

    [Fact]
    public async Task BASYNC008_DiscouragedCharacters_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;

[AsyncApi]
public class TestHub
{
    [Channel(""test topic"")]
    [PublishOperation(OperationId = ""my op"")]
    [Message(typeof(string), MessageId = ""my msg"")]
    public void SendMessage() {}
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.Count(d => d.Id == "BASYNC008").ShouldBe(3);
    }

    [Fact]
    public async Task BASYNC009_MissingDocumentation_ReportsInfo()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;

[AsyncApi]
public class TestHub
{
    [Channel(""test"")]
    [PublishOperation]
    public void SendMessage() {}
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        // AsyncApiAttribute has no Summary/Description; only Operation/Message attributes are checked
        diagnostics.Count(d => d.Id == "BASYNC009").ShouldBe(1);
        diagnostics.ShouldContain(d => d.Id == "BASYNC009" && d.GetMessage().Contains("SendMessage"));
    }

    [Fact]
    public async Task BASYNC009_AsyncApiAttribute_NoWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes;

[AsyncApi]
public class TestHub
{
    [Channel(""test"")]
    [PublishOperation(Summary = ""does a thing"")]
    public void SendMessage() {}
}
";
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.Where(d => d.Id == "BASYNC009").ShouldBeEmpty();
    }

    // Stand-ins for the real AddOpenApi/AddAsyncApi extension methods: the rule matches them by
    // containing type and namespace, so the stubs only need the same names.
    private const string RegistrationStubs = @"
namespace Microsoft.Extensions.DependencyInjection
{
    public class ServiceCollection { }

    public static class OpenApiServiceCollectionExtensions
    {
        public static ServiceCollection AddOpenApi(this ServiceCollection services) => services;
        public static ServiceCollection AddOpenApi(this ServiceCollection services, string documentName) => services;
    }
}

namespace Bielu.AspNetCore.AsyncApi.Extensions
{
    using Microsoft.Extensions.DependencyInjection;

    public static class AsyncApiServiceCollectionExtensions
    {
        public static ServiceCollection AddAsyncApi(this ServiceCollection services) => services;
        public static ServiceCollection AddAsyncApiGeneratedMetadata(this ServiceCollection services) => services;
    }
}
";

    [Fact]
    public async Task BASYNC010_AddOpenApiAfterAddAsyncApi_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAsyncApi();
services.AddOpenApi();
" + RegistrationStubs;
        var diagnostics = await GetDiagnosticsAsync(testCode, OutputKind.ConsoleApplication);
        var diagnostic = diagnostics.Single(d => d.Id == "BASYNC010");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("AddAsyncApi");
        diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length)
            .ShouldBe("AddOpenApi");
    }

    [Fact]
    public async Task BASYNC010_AddOpenApiBeforeAddAsyncApi_NoWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddOpenApi();
services.AddAsyncApi();
" + RegistrationStubs;
        var diagnostics = await GetDiagnosticsAsync(testCode, OutputKind.ConsoleApplication);
        diagnostics.Where(d => d.Id == "BASYNC010").ShouldBeEmpty();
    }

    [Fact]
    public async Task BASYNC010_ChainedAddOpenApiAfterAddAsyncApi_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;

public class Startup
{
    public void ConfigureServices(ServiceCollection services)
    {
        services.AddOpenApi(""internal"").AddAsyncApi().AddOpenApi(""public"");
    }
}
" + RegistrationStubs;
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.Where(d => d.Id == "BASYNC010").Count().ShouldBe(1);
    }

    [Fact]
    public async Task BASYNC010_AddAsyncApiNestedAsAddOpenApiArgument_ReportsWarning()
    {
        // AddAsyncApi appears after AddOpenApi in the source, but runs first: arguments are evaluated before the call.
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
OpenApiServiceCollectionExtensions.AddOpenApi(services.AddAsyncApi());
" + RegistrationStubs;
        var diagnostics = await GetDiagnosticsAsync(testCode, OutputKind.ConsoleApplication);
        diagnostics.Where(d => d.Id == "BASYNC010").Count().ShouldBe(1);
    }

    [Fact]
    public async Task BASYNC010_AddOpenApiNestedAsAddAsyncApiArgument_NoWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
AsyncApiServiceCollectionExtensions.AddAsyncApi(services.AddOpenApi());
" + RegistrationStubs;
        var diagnostics = await GetDiagnosticsAsync(testCode, OutputKind.ConsoleApplication);
        diagnostics.Where(d => d.Id == "BASYNC010").ShouldBeEmpty();
    }

    [Fact]
    public async Task BASYNC010_OtherAsyncApiExtensionBeforeAddOpenApi_NoWarning()
    {
        // AddAsyncApiGeneratedMetadata doesn't register the build-time document provider.
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAsyncApiGeneratedMetadata();
services.AddOpenApi();
services.AddAsyncApi();
" + RegistrationStubs;
        var diagnostics = await GetDiagnosticsAsync(testCode, OutputKind.ConsoleApplication);
        diagnostics.Where(d => d.Id == "BASYNC010").ShouldBeEmpty();
    }

    [Fact]
    public async Task BASYNC010_CallsInDifferentMethods_NoWarning()
    {
        // Order across methods isn't knowable statically, so only one method body is compared.
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;

public class Startup
{
    public void AddEvents(ServiceCollection services) => services.AddAsyncApi();
    public void AddHttp(ServiceCollection services) => services.AddOpenApi();
}
" + RegistrationStubs;
        var diagnostics = await GetDiagnosticsAsync(testCode);
        diagnostics.Where(d => d.Id == "BASYNC010").ShouldBeEmpty();
    }
}
