using System.Text;
using Bielu.Arazzo.Readers;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class ArazzoStreamAndTextReaderTests
{
    private const string Document = """
        arazzo: 1.1.0
        info:
          title: Checkout
          version: 1.0.0
        sourceDescriptions:
          - name: shop
            url: https://example.com/openapi.json
            type: openapi
        workflows:
          - workflowId: checkout
            steps:
              - stepId: pay
                operationId: pay
        """;

    [Fact]
    public void ArazzoStreamReader_Read_ReadsTheWholeStream()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Document));

        // Act
        var result = ArazzoStreamReader.Read(stream);

        // Assert
        result.Diagnostics.Errors.ShouldBeEmpty();
        result.Document.ShouldNotBeNull().Info.Title.ShouldBe("Checkout");
    }

    [Fact]
    public void ArazzoTextReader_Read_ReadsTheWholeReader()
    {
        // Arrange
        using var reader = new StringReader(Document);

        // Act
        var result = ArazzoTextReader.Read(reader);

        // Assert
        result.Document.ShouldNotBeNull().Workflows.ShouldHaveSingleItem().WorkflowId.ShouldBe("checkout");
    }

    [Fact]
    public void Read_NullInput_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ArazzoStreamReader.Read(null!));
        Should.Throw<ArgumentNullException>(() => ArazzoTextReader.Read(null!));
    }
}
