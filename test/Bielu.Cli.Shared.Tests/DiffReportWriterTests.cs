// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared.Diff;
using Shouldly;
using Xunit;

namespace Bielu.Cli.Shared.Tests;

public class DiffReportWriterTests
{
    [Fact]
    public void HasBreakingChanges_WithBreakingChange_ReturnsTrue()
    {
        // Arrange
        var changes = new List<DocumentChange> { new("/a", "removed", ChangeSeverity.Breaking) };

        // Act
        var result = DiffReportWriter.HasBreakingChanges(changes);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasBreakingChanges_WithOnlyNonBreaking_ReturnsFalse()
    {
        // Arrange
        var changes = new List<DocumentChange> { new("/a", "added", ChangeSeverity.NonBreaking) };

        // Act
        var result = DiffReportWriter.HasBreakingChanges(changes);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Write_TextFormat_WithNoChanges_LogsNoChangesDetected()
    {
        // Arrange
        var logger = new FakeCliLogger();

        // Act
        DiffReportWriter.Write([], "text", "Report", logger);

        // Assert
        logger.InfoMessages.ShouldContain("No changes detected.");
    }

    [Fact]
    public void Write_TextFormat_LogsBreakingChangesAsErrors()
    {
        // Arrange
        var changes = new List<DocumentChange> { new("/a", "removed", ChangeSeverity.Breaking) };
        var logger = new FakeCliLogger();

        // Act
        DiffReportWriter.Write(changes, "text", "Report", logger);

        // Assert
        logger.ErrorMessages.ShouldContain(m => m.Contains("removed"));
    }

    [Fact]
    public void Write_MarkdownFormat_WithNoChanges_WritesToConsole()
    {
        // Arrange
        // Markdown goes to stdout rather than the logger, so stdout has to be captured to observe it.
        var logger = new FakeCliLogger();
        var writer = new StringWriter();
        var original = Console.Out;

        // Act
        Console.SetOut(writer);
        try
        {
            DiffReportWriter.Write([], "markdown", "Report", logger);
        }
        finally
        {
            Console.SetOut(original);
        }

        // Assert
        writer.ToString().ShouldContain("No changes detected.");
    }
}
