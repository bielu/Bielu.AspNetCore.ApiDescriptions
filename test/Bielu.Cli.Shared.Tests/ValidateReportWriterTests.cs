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
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "a.json", Errors = [new DiagnosticItem("bad", "/")] },
        };

        ValidateReportWriter.HasFailures(reports, strict: false).ShouldBeTrue();
    }

    [Fact]
    public void HasFailures_WithWarningsOnlyAndNotStrict_ReturnsFalse()
    {
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "a.json", Warnings = [new DiagnosticItem("meh", "/")] },
        };

        ValidateReportWriter.HasFailures(reports, strict: false).ShouldBeFalse();
    }

    [Fact]
    public void HasFailures_WithWarningsOnlyAndStrict_ReturnsTrue()
    {
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "a.json", Warnings = [new DiagnosticItem("meh", "/")] },
        };

        ValidateReportWriter.HasFailures(reports, strict: true).ShouldBeTrue();
    }

    [Fact]
    public void Write_TextFormat_WritesOkForCleanReport()
    {
        var reports = new List<FileDiagnosticReport> { new() { FilePath = "a.json" } };
        var logger = new FakeCliLogger();

        ValidateReportWriter.Write(reports, "text", strict: false, logger);

        logger.InfoMessages.ShouldContain("  OK");
    }

    [Fact]
    public void Write_TextFormat_WithStrictWarning_LogsAsError()
    {
        var reports = new List<FileDiagnosticReport>
        {
            new() { FilePath = "a.json", Warnings = [new DiagnosticItem("meh", "/x")] },
        };
        var logger = new FakeCliLogger();

        ValidateReportWriter.Write(reports, "text", strict: true, logger);

        logger.WarningMessages.ShouldBeEmpty();
        logger.ErrorMessages.ShouldContain(m => m.Contains("meh"));
    }

    [Fact]
    public void Write_UsesCustomVerb()
    {
        var reports = new List<FileDiagnosticReport> { new() { FilePath = "a.json" } };
        var logger = new FakeCliLogger();

        ValidateReportWriter.Write(reports, "text", strict: false, logger, verb: "Linting");

        logger.InfoMessages.ShouldContain(m => m.StartsWith("Linting"));
    }
}
