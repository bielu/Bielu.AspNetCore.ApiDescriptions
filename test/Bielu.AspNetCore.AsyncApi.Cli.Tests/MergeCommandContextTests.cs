// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Unit tests for MergeCommandContext.
/// </summary>
public class MergeCommandContextTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var context = new MergeCommandContext();

        // Assert
        context.Sources.ShouldBeEmpty();
        context.OutputPath.ShouldBe(string.Empty);
        context.Prefixes.ShouldBeEmpty();
        context.Title.ShouldBeNull();
        context.Version.ShouldBeNull();
    }

    [Fact]
    public void Sources_CanBeAdded()
    {
        // Arrange
        var context = new MergeCommandContext();

        // Act
        context.Sources.Add("https://example.com/api1.json");
        context.Sources.Add("/path/to/api2.json");

        // Assert
        context.Sources.Count.ShouldBe(2);
        context.Sources[0].ShouldBe("https://example.com/api1.json");
        context.Sources[1].ShouldBe("/path/to/api2.json");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var context = new MergeCommandContext();

        // Act
        context.OutputPath = "/output/merged.json";
        context.Title = "Merged API";
        context.Version = "1.0.0";
        context.Prefixes.Add("svc1");

        // Assert
        context.OutputPath.ShouldBe("/output/merged.json");
        context.Title.ShouldBe("Merged API");
        context.Version.ShouldBe("1.0.0");
        context.Prefixes.Count.ShouldBe(1);
        context.Prefixes[0].ShouldBe("svc1");
    }
}
