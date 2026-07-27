// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class CommandContextTests
{
    [Fact]
    public void ValidateCommandContext_DefaultValues_AreCorrect()
    {
        var context = new ValidateCommandContext();

        context.Files.ShouldBeEmpty();
        context.Strict.ShouldBeFalse();
        context.Format.ShouldBe("text");
    }

    [Fact]
    public void LintCommandContext_DefaultValues_AreCorrect()
    {
        var context = new LintCommandContext();

        context.Files.ShouldBeEmpty();
        context.Strict.ShouldBeFalse();
        context.Format.ShouldBe("text");
    }

    [Fact]
    public void DiffCommandContext_DefaultValues_AreCorrect()
    {
        var context = new DiffCommandContext();

        context.BasePath.ShouldBe(string.Empty);
        context.HeadPath.ShouldBe(string.Empty);
        context.FailOnBreaking.ShouldBeFalse();
        context.Format.ShouldBe("text");
    }
}
