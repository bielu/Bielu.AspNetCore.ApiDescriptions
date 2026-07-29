// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared.Diagnostics;
using Shouldly;
using Xunit;

namespace Bielu.Cli.Shared.Tests;

public class ValidateReportWriterTests
{
    [Fact]
    public void HasFailures_WithErrors_ReturnsTrue()
    {
        // Arrange
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "a.json", Errors = [new DiagnosticItem("bad", "/")] },
        };

        // Act
        var result = ValidateReportWriter.HasFailures(reports, strict: false);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasFailures_WithWarningsOnlyAndNotStrict_ReturnsFalse()
    {
        // Arrange
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "a.json", Warnings = [new DiagnosticItem("meh", "/")] },
        };

        // Act
        var result = ValidateReportWriter.HasFailures(reports, strict: false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasFailures_WithWarningsOnlyAndStrict_ReturnsTrue()
    {
        // Arrange
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "a.json", Warnings = [new DiagnosticItem("meh", "/")] },
        };

        // Act
        var result = ValidateReportWriter.HasFailures(reports, strict: true);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Write_TextFormat_WritesOkForCleanReport()
    {
        // Arrange
        var reports = new List<FileDiagnosticReport> { new() { FilePath = "a.json" } };
        var logger = new FakeCliLogger();

        // Act
        ValidateReportWriter.Write(reports, "text", strict: false, logger);

        // Assert
        logger.InfoMessages.ShouldContain("  OK");
    }

    [Fact]
    public void Write_TextFormat_WithStrictWarning_LogsAsError()
    {
        // Arrange
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "a.json", Warnings = [new DiagnosticItem("meh", "/x")] },
        };
        var logger = new FakeCliLogger();

        // Act
        ValidateReportWriter.Write(reports, "text", strict: true, logger);

        // Assert
        logger.WarningMessages.ShouldBeEmpty();
        logger.ErrorMessages.ShouldContain(m => m.Contains("meh"));
    }

    [Fact]
    public void Write_UsesCustomVerb()
    {
        // Arrange
        var reports = new List<FileDiagnosticReport> { new() { FilePath = "a.json" } };
        var logger = new FakeCliLogger();

        // Act
        ValidateReportWriter.Write(reports, "text", strict: false, logger, verb: "Linting");

        // Assert
        logger.InfoMessages.ShouldContain(m => m.StartsWith("Linting"));
    }
}
