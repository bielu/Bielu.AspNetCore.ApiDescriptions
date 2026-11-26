// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Runs the tool's entry point in-process with real command lines, so argument parsing and the
/// hand-off to each command's worker are tested, not just the workers.
/// </summary>
public class ProgramTests : IDisposable
{
    private const string BaseDocument = """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Base", "version": "1.0.0" },
            "channels": { "users": { "address": "user/signedup" } },
            "operations": {
                "onUser": { "action": "receive", "channel": { "$ref": "#/channels/users" } }
            }
        }
        """;

    private const string HeadDocument = """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Head", "version": "1.0.0" },
            "channels": { "users": { "address": "user/signedup" } }
        }
        """;

    private readonly string _root;
    private readonly string _basePath;
    private readonly string _headPath;

    public ProgramTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "asyncapi-cli-program-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _basePath = Path.Combine(_root, "base.json");
        _headPath = Path.Combine(_root, "head.json");
        File.WriteAllText(_basePath, BaseDocument);
        File.WriteAllText(_headPath, HeadDocument);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static int Run(params string[] args)
    {
        var entryPoint = typeof(GetDocumentCommandWorker).Assembly.EntryPoint.ShouldNotBeNull();
        try
        {
            return (int)entryPoint.Invoke(null, [args])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    [Theory]
    [InlineData]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Main_NoArgumentsOrHelp_PrintsUsageAndSucceeds(params string[] args)
    {
        Run(args).ShouldBe(0);
    }

    [Fact]
    public void Main_UnknownCommand_Fails()
    {
        Run("publish").ShouldBe(1);
    }

    [Theory]
    [InlineData("getdocument", "--bogus")]
    [InlineData("merge", "--bogus")]
    [InlineData("validate", "--bogus")]
    [InlineData("diff", "--bogus")]
    public void Main_UnknownOption_Fails(string command, string option)
    {
        Run(command, option).ShouldBe(1);
    }

    [Theory]
    [InlineData("getdocument", "--assembly")]
    [InlineData("getdocument", "--assembly-path")]
    [InlineData("getdocument", "--output")]
    [InlineData("getdocument", "--project")]
    [InlineData("getdocument", "--document")]
    [InlineData("getdocument", "--file-list")]
    [InlineData("getdocument", "--file-name")]
    [InlineData("merge", "--source")]
    [InlineData("merge", "--output")]
    [InlineData("merge", "--prefix")]
    [InlineData("merge", "--title")]
    [InlineData("merge", "--version")]
    [InlineData("validate", "--file")]
    [InlineData("validate", "--format")]
    [InlineData("diff", "--base")]
    [InlineData("diff", "--head")]
    [InlineData("diff", "--format")]
    public void Main_OptionWithoutValue_FailsInsteadOfThrowing(string command, string option)
    {
        Run(command, option).ShouldBe(1);
    }

    [Fact]
    public void GetDocument_WithoutAssembly_Fails()
    {
        Run("getdocument", "--output", _root).ShouldBe(1);
    }

    [Fact]
    public void GetDocument_WithoutOutput_Fails()
    {
        Run("getdocument", "--assembly", "StreetlightsAPI").ShouldBe(1);
    }

    [Fact]
    public void GetDocument_HostBuilderApp_WritesTheDocumentAndFileList()
    {
        // Arrange
        var output = Path.Combine(_root, "out");
        var fileList = Path.Combine(_root, "files.cache");

        // Act
        // The assembly path has no runtimeconfig next to it, so the command runs in-process and loads
        // StreetlightsAPI from the test's own output by name.
        var exitCode = Run(
            "getdocument",
            "--assembly", "StreetlightsAPI",
            "--assembly-path", Path.Combine(_root, "StreetlightsAPI.dll"),
            "--output", output,
            "--project", "Streetlights",
            "--document", "v1",
            "--file-list", fileList,
            "--file-name", "streetlights");

        // Assert
        exitCode.ShouldBe(0);
        var documentPath = Path.Combine(output, "streetlights.json");
        File.ReadAllText(documentPath).ShouldContain("\"asyncapi\"");
        File.ReadAllText(fileList).Trim().ShouldBe(documentPath);
    }

    [Fact]
    public void Merge_WithoutSource_Fails()
    {
        Run("merge", "--output", Path.Combine(_root, "merged.json")).ShouldBe(1);
    }

    [Fact]
    public void Merge_WithoutOutput_Fails()
    {
        Run("merge", "--source", _basePath).ShouldBe(1);
    }

    [Fact]
    public void Merge_TwoSources_WritesMergedDocumentWithTitleAndVersion()
    {
        // Arrange
        var output = Path.Combine(_root, "merged.json");

        // Act
        var exitCode = Run(
            "merge",
            "--source", _basePath,
            "--source", _headPath,
            "--prefix", "base",
            "--prefix", "head",
            "--title", "Gateway",
            "--version", "2.0.0",
            "--output", output);

        // Assert
        exitCode.ShouldBe(0);
        var merged = JsonNode.Parse(File.ReadAllText(output))!;
        merged["info"]!["title"]!.GetValue<string>().ShouldBe("Gateway");
        merged["info"]!["version"]!.GetValue<string>().ShouldBe("2.0.0");
    }

    [Fact]
    public void Validate_WithoutFile_Fails()
    {
        Run("validate", "--strict").ShouldBe(1);
    }

    [Fact]
    public void Validate_ValidFile_SucceedsInStrictJsonMode()
    {
        Run("validate", "--file", _headPath, "--strict", "--format", "json").ShouldBe(0);
    }

    [Theory]
    [InlineData("--base")]
    [InlineData("--head")]
    public void Diff_WithOnlyOneSide_Fails(string option)
    {
        Run("diff", option, _basePath).ShouldBe(1);
    }

    [Fact]
    public void Diff_BreakingChange_FailsOnlyWithFailOnBreaking()
    {
        Run("diff", "--base", _basePath, "--head", _headPath, "--format", "json").ShouldBe(0);
        Run("diff", "--base", _basePath, "--head", _headPath, "--fail-on-breaking").ShouldBe(1);
    }
}
