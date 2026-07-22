// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

public class ValidateCommandContextTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var context = new ValidateCommandContext();

        context.Files.ShouldBeEmpty();
        context.Strict.ShouldBeFalse();
        context.Format.ShouldBe("text");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var context = new ValidateCommandContext
        {
            Strict = true,
            Format = "json"
        };
        context.Files.Add("api.json");

        context.Files.ShouldContain("api.json");
        context.Strict.ShouldBeTrue();
        context.Format.ShouldBe("json");
    }
}
