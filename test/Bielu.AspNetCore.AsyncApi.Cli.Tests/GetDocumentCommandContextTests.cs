using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Unit tests for GetDocumentCommandContext.
/// </summary>
public class GetDocumentCommandContextTests
{
    [Fact]
    public void DefaultValues_AreEmptyStrings()
    {
        // Arrange & Act
        var context = new GetDocumentCommandContext();

        // Assert
        context.AssemblyName.ShouldBe(string.Empty);
        context.AssemblyPath.ShouldBe(string.Empty);
        context.OutputDirectory.ShouldBe(string.Empty);
        context.ProjectName.ShouldBe(string.Empty);
        context.FileListPath.ShouldBe(string.Empty);
    }

    [Fact]
    public void NullableProperties_DefaultToNull()
    {
        // Arrange & Act
        var context = new GetDocumentCommandContext();

        // Assert
        context.DocumentName.ShouldBeNull();
        context.FileName.ShouldBeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var context = new GetDocumentCommandContext();

        // Act
        context.AssemblyName = "MyApp";
        context.AssemblyPath = "/path/to/MyApp.dll";
        context.OutputDirectory = "/output";
        context.ProjectName = "MyProject";
        context.DocumentName = "v1";
        context.FileListPath = "/cache/files.cache";
        context.FileName = "custom-name";

        // Assert
        context.AssemblyName.ShouldBe("MyApp");
        context.AssemblyPath.ShouldBe("/path/to/MyApp.dll");
        context.OutputDirectory.ShouldBe("/output");
        context.ProjectName.ShouldBe("MyProject");
        context.DocumentName.ShouldBe("v1");
        context.FileListPath.ShouldBe("/cache/files.cache");
        context.FileName.ShouldBe("custom-name");
    }
}
