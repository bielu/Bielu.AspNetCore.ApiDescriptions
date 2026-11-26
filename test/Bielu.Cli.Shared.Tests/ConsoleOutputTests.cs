// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.Cli.Shared.Diagnostics;
using Bielu.Cli.Shared.Diff;
using Shouldly;
using Xunit;

namespace Bielu.Cli.Shared.Tests;

/// <summary>Tests that swap <see cref="Console.Out"/>/<see cref="Console.Error"/> run in this one collection, never at once.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCollection
{
    public const string Name = "Console";
}

[Collection(ConsoleCollection.Name)]
public class ConsoleOutputTests
{
    private static string CaptureOut(Action action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }

    private static string CaptureError(Action action)
    {
        var original = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetError(original);
        }

        return writer.ToString();
    }

    private static readonly List<DocumentChange> MixedChanges =
    [
        new("channels/old", "Channel 'old' was removed.", ChangeSeverity.Breaking),
        new("channels/new", "Channel 'new' was added.", ChangeSeverity.NonBreaking),
    ];

    [Fact]
    public void ConsoleCliLogger_InfoAndWarning_WriteToStandardOutput()
    {
        // Arrange
        var logger = new ConsoleCliLogger();

        // Act
        var output = CaptureOut(() =>
        {
            logger.Info("started");
            logger.Warning("careful");
        });

        // Assert
        output.ShouldContain("info: started");
        output.ShouldContain("warn: careful");
    }

    [Fact]
    public void ConsoleCliLogger_Error_WritesToStandardError()
    {
        // Arrange
        var logger = new ConsoleCliLogger();

        // Act
        var output = CaptureError(() => logger.Error("broken"));

        // Assert
        output.ShouldContain("error: broken");
    }

    [Fact]
    public void DiffReportWriter_JsonFormat_WritesTheChangesAsIndentedJson()
    {
        // Arrange
        var logger = new FakeCliLogger();

        // Act
        var output = CaptureOut(() => DiffReportWriter.Write(MixedChanges, "JSON", "Report", logger));

        // Assert
        var changes = JsonSerializer.Deserialize<List<DocumentChange>>(output).ShouldNotBeNull();
        changes.ShouldBe(MixedChanges);
        output.ShouldContain(Environment.NewLine + "  ");
        logger.InfoMessages.ShouldBeEmpty();
    }

    [Fact]
    public void DiffReportWriter_MarkdownFormat_WritesATablePerSeverity()
    {
        // Arrange
        var logger = new FakeCliLogger();

        // Act
        var output = CaptureOut(() => DiffReportWriter.Write(MixedChanges, "markdown", "AsyncAPI changes", logger));

        // Assert
        output.ShouldContain("### AsyncAPI changes");
        output.ShouldContain("#### Breaking Changes");
        output.ShouldContain("#### NonBreaking Changes");
        output.ShouldContain("| `channels/old` | Channel 'old' was removed. |");
        output.ShouldContain("| `channels/new` | Channel 'new' was added. |");
    }

    [Fact]
    public void DiffReportWriter_TextFormat_LogsNonBreakingChangesAsInfo()
    {
        // Arrange
        var logger = new FakeCliLogger();

        // Act
        DiffReportWriter.Write(MixedChanges, "text", "Report", logger);

        // Assert
        logger.InfoMessages.ShouldContain("Breaking:");
        logger.InfoMessages.ShouldContain("NonBreaking:");
        logger.InfoMessages.ShouldContain("  - channels/new: Channel 'new' was added.");
        logger.ErrorMessages.ShouldBe(["  - channels/old: Channel 'old' was removed."]);
    }

    [Fact]
    public void ValidateReportWriter_JsonFormat_WritesTheReports()
    {
        // Arrange
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "doc.json", Errors = [new DiagnosticItem("Missing info.", "#/info")] },
        };
        var logger = new FakeCliLogger();

        // Act
        var output = CaptureOut(() => ValidateReportWriter.Write(reports, "json", strict: false, logger));

        // Assert
        output.ShouldContain("doc.json");
        output.ShouldContain("Missing info.");
        logger.InfoMessages.ShouldBeEmpty();
    }

    [Fact]
    public void CliJsonDefaults_Indented_WritesIndentedJson()
    {
        CliJsonDefaults.Indented.WriteIndented.ShouldBeTrue();
    }
}
