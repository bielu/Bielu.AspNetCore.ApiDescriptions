// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Overlay.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Cli.Tests;

public class CommandContextTests
{
    [Fact]
    public void ApplyCommandContext_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var context = new ApplyCommandContext();

        // Assert
        context.FilePath.ShouldBe(string.Empty);
        context.Overlays.ShouldBeEmpty();
        // Empty rather than a path: apply writes to standard output unless told otherwise, and infers
        // its format from the output extension when one is given.
        context.OutputPath.ShouldBe(string.Empty);
        context.Format.ShouldBe(string.Empty);
        context.Strict.ShouldBeFalse();
    }

    [Fact]
    public void ValidateCommandContext_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var context = new ValidateCommandContext();

        // Assert
        context.Files.ShouldBeEmpty();
        context.Strict.ShouldBeFalse();
        context.Format.ShouldBe("text");
    }
}
