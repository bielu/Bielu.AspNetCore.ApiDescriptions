using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs;

public class XmlDocumentationIdBuilderTests
{
    [Fact]
    public void CreateIdForMember_Type_ReturnsCorrectId()
    {
        // Arrange
        var type = typeof(SampleType);

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(type);

        // Assert
        id.ShouldBe("T:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType");
    }

    [Fact]
    public void CreateIdForMember_NestedType_ReturnsCorrectId()
    {
        // Arrange
        var type = typeof(SampleType.NestedType);

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(type);

        // Assert
        id.ShouldBe("T:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.NestedType");
    }

    [Fact]
    public void CreateIdForMember_Method_ReturnsCorrectId()
    {
        // Arrange
        var method = typeof(SampleType).GetMethod(nameof(SampleType.SampleMethod));

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(method);

        // Assert
        id.ShouldBe("M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.SampleMethod");
    }

    [Fact]
    public void CreateIdForMember_MethodWithParameters_ReturnsCorrectId()
    {
        // Arrange
        var method = typeof(SampleType).GetMethod(nameof(SampleType.MethodWithParameters));

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(method);

        // Assert
        id.ShouldBe("M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.MethodWithParameters(System.String,System.Int32)");
    }

    [Fact]
    public void CreateIdForMember_Property_ReturnsCorrectId()
    {
        // Arrange
        var property = typeof(SampleType).GetProperty(nameof(SampleType.SampleProperty));

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(property);

        // Assert
        id.ShouldBe("P:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.SampleProperty");
    }

    [Fact]
    public void CreateIdForMember_GenericType_ReturnsCorrectId()
    {
        // Arrange
        var type = typeof(GenericSample<string>);

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(type);

        // Assert
        id.ShouldBe("T:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.GenericSample{System.String}");
    }

    [Fact]
    public void CreateIdForMember_GenericMethod_ReturnsCorrectId()
    {
        // Arrange
        var method = typeof(SampleType).GetMethod(nameof(SampleType.GenericMethod));

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(method);

        // Assert
        id.ShouldBe("M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.GenericMethod``1(``0)");
    }
}

public class SampleType
{
    public string SampleProperty { get; set; }
    public void SampleMethod() { }
    public void MethodWithParameters(string s, int i) { }
    public void GenericMethod<T>(T value) { }

    public class NestedType { }
}

public class GenericSample<T> { }
