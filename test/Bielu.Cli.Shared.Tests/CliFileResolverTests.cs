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
        var result = CliFileResolver.ExpandFilePatterns(["missing.json"]);

        result.ShouldBe(["missing.json"]);
    }

    [Fact]
    public void ExpandFilePatterns_WithGlob_ExpandsToMatchingFiles()
    {
        var pattern = Path.Combine(_tempDir, "*.json");

        var result = CliFileResolver.ExpandFilePatterns([pattern]);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(f => f.EndsWith(".json"));
    }

    [Fact]
    public void ExpandFilePatterns_MixesLiteralsAndGlobs()
    {
        var pattern = Path.Combine(_tempDir, "*.yaml");
        var literal = "explicit.json";

        var result = CliFileResolver.ExpandFilePatterns([literal, pattern]);

        result.Count.ShouldBe(2);
        result.ShouldContain(literal);
    }
}
