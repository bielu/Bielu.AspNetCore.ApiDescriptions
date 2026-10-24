using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCollection
{
    // Program writes through the process-wide Console, so these tests must not run alongside each other.
    public const string Name = "Console";
}

/// <summary>
/// Drives the <c>dotnet asyncapi</c> entry point with real command lines: argument parsing, required-option
/// validation and dispatch to each command's worker.
/// </summary>
[Collection(ConsoleCollection.Name)]
public sealed class ProgramTests : IDisposable
{
    private const int Success = 0;
    private const int Failure = 1;

    private const string MinimalDocument = """
        { "asyncapi": "3.0.0", "info": { "title": "A", "version": "1.0.0" }, "channels": { "orders": { "address": "orders" } } }
        """;

    private const string ChangedDocument = """
        { "asyncapi": "3.0.0", "info": { "title": "A", "version": "2.0.0" }, "channels": { "shipments": { "address": "shipments" } } }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "asyncapi-cli-program-tests", Guid.NewGuid().ToString("N"));
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _output = new();

    public ProgramTests()
    {
        Directory.CreateDirectory(_directory);
        Console.SetOut(_output);
        Console.SetError(_output);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Directory.Delete(_directory, recursive: true);
    }

    private static int Run(params string[] args)
    {
        var entryPoint = typeof(GetDocumentCommandWorker).Assembly.EntryPoint.ShouldNotBeNull();
        return (int)entryPoint.Invoke(null, [args])!;
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Theory]
    [InlineData]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Main_NoArgumentsOrHelp_PrintsUsage(params string[] args)
    {
        // Act
        var exitCode = Run(args);

        // Assert
        exitCode.ShouldBe(Success);
        _output.ToString().ShouldContain("Usage: dotnet asyncapi <command> [options]");
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("getdocument", "--bogus")]
    [InlineData("merge", "--bogus")]
    [InlineData("validate", "--bogus")]
    [InlineData("diff", "--bogus")]
    public void Main_UnknownCommandOrArgument_Fails(params string[] args)
    {
        // Act & Assert
        Run(args).ShouldBe(Failure);
    }

    [Theory]
    [InlineData("getdocument", "--output", "out")]
    [InlineData("getdocument", "--assembly", "App")]
    [InlineData("merge", "--output", "merged.json")]
    [InlineData("merge", "--source", "a.json")]
    [InlineData("validate", "--strict")]
    [InlineData("diff", "--base", "a.json")]
    public void Main_MissingRequiredOption_Fails(params string[] args)
    {
        // Act & Assert
        Run(args).ShouldBe(Failure);
    }

    [Theory]
    [InlineData("getdocument", "--assembly")]
    [InlineData("getdocument", "--assembly", "App", "--output")]
    [InlineData("getdocument", "--assembly", "App", "--output", "out", "--file-name")]
    [InlineData("merge", "--source")]
    [InlineData("merge", "--source", "a.json", "--output")]
    [InlineData("merge", "--source", "a.json", "--prefix")]
    [InlineData("merge", "--source", "a.json", "--title")]
    [InlineData("merge", "--source", "a.json", "--version")]
    [InlineData("validate", "--file")]
    [InlineData("validate", "--file", "a.json", "--format")]
    [InlineData("diff", "--base")]
    [InlineData("diff", "--base", "a.json", "--head")]
    [InlineData("diff", "--base", "a.json", "--head", "b.json", "--format")]
    public void Main_OptionWithoutValue_FailsWithoutThrowing(params string[] args)
    {
        // Act & Assert
        Run(args).ShouldBe(Failure);
        _output.ToString().ShouldContain("requires a value");
    }

    [Fact]
    public void Main_GetDocument_GeneratesTheAppsDocument()
    {
        // Arrange
        var assembly = Assembly.Load("StreetlightsAPI");
        var output = Path.Combine(_directory, "docs");

        // Act
        var exitCode = Run(
            "getdocument",
            "--assembly", "StreetlightsAPI",
            "--assembly-path", assembly.Location,
            "--output", output,
            "--document", "v1",
            "--file-list", Path.Combine(_directory, "files.txt"));

        // Assert: --project defaults to the assembly name.
        exitCode.ShouldBe(Success);
        File.Exists(Path.Combine(output, "StreetlightsAPI.json")).ShouldBeTrue();
    }

    [Fact]
    public void Main_Merge_WritesTheMergedDocument()
    {
        // Arrange
        var first = WriteFile("a.json", MinimalDocument);
        var second = WriteFile("b.json", ChangedDocument);
        var output = Path.Combine(_directory, "merged.json");

        // Act
        var exitCode = Run(
            "merge",
            "--source", first, "--prefix", "a",
            "--source", second, "--prefix", "b",
            "--output", output,
            "--title", "Gateway",
            "--version", "9.9.9");

        // Assert
        exitCode.ShouldBe(Success);
        var merged = File.ReadAllText(output);
        merged.ShouldContain("Gateway");
        merged.ShouldContain("9.9.9");
    }

    [Fact]
    public void Main_Validate_ValidDocumentSucceeds()
    {
        // Arrange
        var file = WriteFile("valid.json", MinimalDocument);

        // Act & Assert
        Run("validate", "--file", file, "--strict", "--format", "json").ShouldBe(Success);
    }

    [Fact]
    public void Main_Diff_BreakingChangeFailsOnlyWhenAsked()
    {
        // Arrange
        var @base = WriteFile("base.json", MinimalDocument);
        var head = WriteFile("head.json", ChangedDocument);

        // Act & Assert
        Run("diff", "--base", @base, "--head", head, "--format", "markdown").ShouldBe(Success);
        Run("diff", "--base", @base, "--head", head, "--fail-on-breaking").ShouldBe(Failure);
    }
}
