using Bielu.Overlay.Readers;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

public class OverlayTextReaderTests
{
    [Fact]
    public void Read_ReadsTheWholeReader()
    {
        // Arrange
        using var reader = new StringReader("""
            overlay: 1.1.0
            info: { title: Strip internal, version: 1.0.0 }
            actions:
              - target: $.channels.internal
                remove: true
            """);

        // Act
        var result = OverlayTextReader.Read(reader);

        // Assert
        result.HasErrors.ShouldBeFalse();
        result.Document.ShouldNotBeNull().Actions.ShouldHaveSingleItem().Remove.ShouldBe(true);
    }

    [Fact]
    public void Read_NullReader_Throws()
    {
        Should.Throw<ArgumentNullException>(() => OverlayTextReader.Read(null!));
    }
}
