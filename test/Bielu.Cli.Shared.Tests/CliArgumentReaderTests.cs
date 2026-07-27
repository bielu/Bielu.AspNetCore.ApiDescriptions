// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared;
using Shouldly;
using Xunit;

namespace Bielu.Cli.Shared.Tests;

public class CliArgumentReaderTests
{
    [Fact]
    public void TryReadValue_WithValueAvailable_ReturnsTrueAndAdvancesIndex()
    {
        var args = new[] { "--file", "doc.json" };
        var logger = new FakeCliLogger();
        var index = 0;

        var result = CliArgumentReader.TryReadValue(args, ref index, "--file", logger, out var value);

        result.ShouldBeTrue();
        value.ShouldBe("doc.json");
        index.ShouldBe(1);
        logger.ErrorMessages.ShouldBeEmpty();
    }

    [Fact]
    public void TryReadValue_AtEndOfArgs_ReturnsFalseAndLogsError()
    {
        var args = new[] { "--file" };
        var logger = new FakeCliLogger();
        var index = 0;

        var result = CliArgumentReader.TryReadValue(args, ref index, "--file", logger, out var value);

        result.ShouldBeFalse();
        value.ShouldBe(string.Empty);
        logger.ErrorMessages.ShouldHaveSingleItem();
        logger.ErrorMessages[0].ShouldContain("--file");
    }
}
