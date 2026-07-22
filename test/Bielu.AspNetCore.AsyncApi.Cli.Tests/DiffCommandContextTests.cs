// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

public class DiffCommandContextTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var context = new DiffCommandContext();

        context.BasePath.ShouldBe(string.Empty);
        context.HeadPath.ShouldBe(string.Empty);
        context.FailOnBreaking.ShouldBeFalse();
        context.Format.ShouldBe("text");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var context = new DiffCommandContext
        {
            BasePath = "old.json",
            HeadPath = "new.json",
            FailOnBreaking = true,
            Format = "markdown"
        };

        context.BasePath.ShouldBe("old.json");
        context.HeadPath.ShouldBe("new.json");
        context.FailOnBreaking.ShouldBeTrue();
        context.Format.ShouldBe("markdown");
    }
}
