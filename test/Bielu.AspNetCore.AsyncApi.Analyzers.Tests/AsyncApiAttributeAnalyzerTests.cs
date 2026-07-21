using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Shouldly;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Tests;

public class AsyncApiAttributeAnalyzerTests
{
    private async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Bielu.AspNetCore.AsyncApi.Attributes.Attributes.AsyncApiAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName("System.Runtime")).Location),
            MetadataReference.CreateFromFile(System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName("netstandard")).Location),
        };

        var compilation = CSharpCompilation.Create("Test", new[] { syntaxTree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzer = new AsyncApiAttributeAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task BASYNC001_MissingAsyncApiAttribute_OnType_ReportsWarning()
    {
        var testCode = @"
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

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
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

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
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

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
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

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
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
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
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
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
}
