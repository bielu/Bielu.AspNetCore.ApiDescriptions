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
        var changes = new List<DocumentChange> { new("/a", "removed", ChangeSeverity.Breaking) };

        DiffReportWriter.HasBreakingChanges(changes).ShouldBeTrue();
    }

    [Fact]
    public void HasBreakingChanges_WithOnlyNonBreaking_ReturnsFalse()
    {
        var changes = new List<DocumentChange> { new("/a", "added", ChangeSeverity.NonBreaking) };

        DiffReportWriter.HasBreakingChanges(changes).ShouldBeFalse();
    }

    [Fact]
    public void Write_TextFormat_WithNoChanges_LogsNoChangesDetected()
    {
        var logger = new FakeCliLogger();

        DiffReportWriter.Write([], "text", "Report", logger);

        logger.InfoMessages.ShouldContain("No changes detected.");
    }

    [Fact]
    public void Write_TextFormat_LogsBreakingChangesAsErrors()
    {
        var changes = new List<DocumentChange> { new("/a", "removed", ChangeSeverity.Breaking) };
        var logger = new FakeCliLogger();

        DiffReportWriter.Write(changes, "text", "Report", logger);

        logger.ErrorMessages.ShouldContain(m => m.Contains("removed"));
    }

    [Fact]
    public void Write_MarkdownFormat_WithNoChanges_WritesToConsole()
    {
        var logger = new FakeCliLogger();
        var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            DiffReportWriter.Write([], "markdown", "Report", logger);
        }
        finally
        {
            Console.SetOut(original);
        }

        writer.ToString().ShouldContain("No changes detected.");
    }
}
