using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Unit tests for GetDocumentCommandWorker.
/// </summary>
public class GetDocumentCommandWorkerTests
{
    [Fact]
    public void Constructor_ThrowsForNullContext()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new GetDocumentCommandWorker(
                null!,
                _ => { },
                _ => { },
                _ => { }));
    }

    [Fact]
    public void Process_WithInvalidAssemblyName_ThrowsFileNotFoundException()
    {
        // Arrange
        var context = new GetDocumentCommandContext
        {
            AssemblyName = "NonExistentAssembly_12345",
            OutputDirectory = "/tmp/output"
        };

        var worker = new GetDocumentCommandWorker(
            context,
            writeInfo: _ => { },
            writeWarning: _ => { },
            writeError: _ => { });

        // Act & Assert - Assembly.Load throws when the assembly cannot be found
        Should.Throw<FileNotFoundException>(() => worker.Process());
    }

    [Fact]
    public void Process_WithThisTestAssembly_ReturnsNonZeroExitCode()
    {
        // Arrange - Use this test assembly which has no entry point / no IDocumentProvider
        var context = new GetDocumentCommandContext
        {
            AssemblyName = typeof(GetDocumentCommandWorkerTests).Assembly.GetName().Name!,
            AssemblyPath = typeof(GetDocumentCommandWorkerTests).Assembly.Location,
            OutputDirectory = "/tmp/output"
        };

        var errors = new List<string>();
        var worker = new GetDocumentCommandWorker(
            context,
            writeInfo: _ => { },
            writeWarning: _ => { },
            writeError: msg => errors.Add(msg));

        // Act
        var result = worker.Process();

        // Assert - should fail because test assembly doesn't have proper host builder patterns
        result.ShouldNotBe(0);
        errors.ShouldNotBeEmpty();
    }

    private const string HostBuilderAppAssembly = "StreetlightsAPI";

    private static string CreateOutputDirectory() =>
        Path.Combine(Path.GetTempPath(), "asyncapi-cli-tests", Guid.NewGuid().ToString("N"));

    private static (int ExitCode, List<string> Errors) RunWorker(GetDocumentCommandContext context)
    {
        var errors = new List<string>();
        var worker = new GetDocumentCommandWorker(
            context,
            writeInfo: _ => { },
            writeWarning: _ => { },
            writeError: errors.Add);
        return (worker.Process(), errors);
    }

    [Fact]
    public void Process_HostBuilderApp_WritesDefaultDocumentAndFileList()
    {
        // Arrange
        var outputDirectory = CreateOutputDirectory();
        var fileListPath = Path.Combine(outputDirectory, "files.cache");
        var context = new GetDocumentCommandContext
        {
            AssemblyName = HostBuilderAppAssembly,
            OutputDirectory = outputDirectory,
            ProjectName = "Streetlights",
            DocumentName = "v1",
            FileListPath = fileListPath,
        };

        try
        {
            // Act
            var (exitCode, errors) = RunWorker(context);

            // Assert
            errors.ShouldBeEmpty();
            exitCode.ShouldBe(0);
            var documentPath = Path.Combine(outputDirectory, "Streetlights.json");
            File.Exists(documentPath).ShouldBeTrue();
            File.ReadAllText(documentPath).ShouldContain("\"asyncapi\"");
            File.ReadAllText(fileListPath).Trim().ShouldBe(documentPath);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Process_CustomFileName_UsesItInsteadOfProjectName()
    {
        // Arrange
        var outputDirectory = CreateOutputDirectory();
        var context = new GetDocumentCommandContext
        {
            AssemblyName = HostBuilderAppAssembly,
            OutputDirectory = outputDirectory,
            ProjectName = "Streetlights",
            DocumentName = "v1",
            FileName = "custom-name_1",
        };

        try
        {
            // Act
            var (exitCode, _) = RunWorker(context);

            // Assert
            exitCode.ShouldBe(0);
            File.Exists(Path.Combine(outputDirectory, "custom-name_1.json")).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Process_UnknownDocumentName_ReturnsFailureAndListsAvailableDocuments()
    {
        // Arrange
        var context = new GetDocumentCommandContext
        {
            AssemblyName = HostBuilderAppAssembly,
            OutputDirectory = CreateOutputDirectory(),
            ProjectName = "Streetlights",
            DocumentName = "does-not-exist",
        };

        // Act
        var (exitCode, errors) = RunWorker(context);

        // Assert
        exitCode.ShouldBe(10);
        errors.ShouldHaveSingleItem().ShouldContain("Document 'does-not-exist' was not found");
        Directory.Exists(context.OutputDirectory).ShouldBeFalse();
    }

    [Fact]
    public void Process_InvalidFileName_ReturnsFailure()
    {
        // Arrange
        var context = new GetDocumentCommandContext
        {
            AssemblyName = HostBuilderAppAssembly,
            OutputDirectory = CreateOutputDirectory(),
            ProjectName = "Streetlights",
            FileName = "not valid!",
        };

        // Act
        var (exitCode, errors) = RunWorker(context);

        // Assert
        exitCode.ShouldBe(10);
        errors.ShouldHaveSingleItem().ShouldContain("file name format is invalid");
        Directory.Exists(context.OutputDirectory).ShouldBeFalse();
    }

    [Fact]
    public void GetDocumentPath_DefaultDocument_OmitsDocumentName()
    {
        // Act
        var path = GetDocumentCommandWorker.GetDocumentPath("v1", "Api", "out");

        // Assert
        path.ShouldBe(Path.Combine("out", "Api.json"));
    }

    [Theory]
    [InlineData("v2", "Api_v2.json")]
    [InlineData("internal/v2", "Api_internal_v2.json")]
    [InlineData("a....b", "Api_a.b.json")]
    public void GetDocumentPath_NamedDocument_SanitizesDocumentName(string documentName, string expectedFileName)
    {
        // Act
        var path = GetDocumentCommandWorker.GetDocumentPath(documentName, "Api", "out");

        // Assert
        path.ShouldBe(Path.Combine("out", expectedFileName));
    }
}
