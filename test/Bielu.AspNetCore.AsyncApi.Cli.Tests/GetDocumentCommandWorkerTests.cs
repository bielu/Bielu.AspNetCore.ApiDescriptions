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
}
