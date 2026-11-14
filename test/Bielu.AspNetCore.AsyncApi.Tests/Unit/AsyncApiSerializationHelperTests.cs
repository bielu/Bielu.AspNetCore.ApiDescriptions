using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for AsyncApiSerializationHelper.
/// Validates that serialization methods produce correct output format.
/// </summary>
public class AsyncApiSerializationHelperTests
{
    private static AsyncApiDocument CreateSimpleDocument()
    {
        return new AsyncApiDocument
        {
            Info = new AsyncApiInfo
            {
                Title = "Test API",
                Version = "1.0.0"
            }
        };
    }

    [Fact]
    public void SerializeV2ToJson_NonAsciiAndHtmlCharacters_AreWrittenUnescaped()
    {
        // Arrange
        var document = new AsyncApiDocument { Info = new AsyncApiInfo { Title = "Café <lights> + more", Version = "1.0.0" } };

        // Act
        var result = AsyncApiSerializationHelper.SerializeV2ToJson(document);

        // Assert
        result.ShouldContain("Café <lights> + more");
    }

    [Fact]
    public void SerializeV2ToJson_IsIndentedLikeV3()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV2ToJson(document);

        // Assert: ByteBard's writer indents, as for 3.x; 2.x used to be re-minified onto one line.
        result.Split('\n').Length.ShouldBeGreaterThan(3);
    }

    [Fact]
    public void SerializeV2ToJson_ProducesNonEmptyJson()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV2ToJson(document);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SerializeV2ToJson_ContainsAsyncApiVersionField()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV2ToJson(document);

        // Assert
        result.ShouldContain("asyncapi");
    }

    [Fact]
    public void SerializeV2ToJson_ContainsInfoSection()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV2ToJson(document);

        // Assert
        result.ShouldContain("info");
        result.ShouldContain("Test API");
    }

    [Fact]
    public void SerializeV3ToJson_ProducesNonEmptyJson()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV3ToJson(document);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SerializeV3ToJson_ContainsAsyncApiVersionField()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV3ToJson(document);

        // Assert
        result.ShouldContain("asyncapi");
    }

    [Fact]
    public void SerializeV2ToYaml_ProducesNonEmptyOutput()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV2ToYaml(document);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SerializeV3ToYaml_ProducesNonEmptyOutput()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV3ToYaml(document);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SerializeV3ToYaml_ContainsAsyncApiField()
    {
        // Arrange
        var document = CreateSimpleDocument();

        // Act
        var result = AsyncApiSerializationHelper.SerializeV3ToYaml(document);

        // Assert
        result.ShouldContain("asyncapi");
    }
}
