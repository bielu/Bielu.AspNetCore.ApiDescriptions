// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared;
using Shouldly;
using Xunit;

namespace Bielu.Cli.Shared.Tests;

public class CliFileResolverTests : IDisposable
{
    private readonly string _tempDir;

    public CliFileResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cli_shared_glob_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "a.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "b.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "c.yaml"), "{}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ExpandFilePatterns_WithLiteralPath_ReturnsItUnchanged()
    {
        // Arrange
        string[] patterns = ["missing.json"];

        // Act
        var result = CliFileResolver.ExpandFilePatterns(patterns);

        // Assert
        // Passed through even though it does not exist, so the caller can report a clear "file not found".
        result.ShouldBe(["missing.json"]);
    }

    [Fact]
    public void ExpandFilePatterns_WithGlob_ExpandsToMatchingFiles()
    {
        // Arrange
        var pattern = Path.Combine(_tempDir, "*.json");

        // Act
        var result = CliFileResolver.ExpandFilePatterns([pattern]);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(f => f.EndsWith(".json"));
    }

    [Fact]
    public void ExpandFilePatterns_MixesLiteralsAndGlobs()
    {
        // Arrange
        var pattern = Path.Combine(_tempDir, "*.yaml");
        var literal = "explicit.json";

        // Act
        var result = CliFileResolver.ExpandFilePatterns([literal, pattern]);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(literal);
    }
}
