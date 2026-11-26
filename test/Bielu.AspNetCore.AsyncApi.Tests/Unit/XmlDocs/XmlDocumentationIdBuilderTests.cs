using System.Reflection;
using System.Xml.Linq;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs;

public class XmlDocumentationIdBuilderTests
{
    private const string Ns = "Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs";

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
        var method = typeof(SampleType).GetMethod(nameof(SampleType.SampleMethod))!;

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(method);

        // Assert
        id.ShouldBe("M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.SampleMethod");
    }

    [Fact]
    public void CreateIdForMember_MethodWithParameters_ReturnsCorrectId()
    {
        // Arrange
        var method = typeof(SampleType).GetMethod(nameof(SampleType.MethodWithParameters))!;

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(method);

        // Assert
        id.ShouldBe("M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.MethodWithParameters(System.String,System.Int32)");
    }

    [Fact]
    public void CreateIdForMember_Property_ReturnsCorrectId()
    {
        // Arrange
        var property = typeof(SampleType).GetProperty(nameof(SampleType.SampleProperty))!;

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(property);

        // Assert
        id.ShouldBe("P:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.SampleProperty");
    }

    [Fact]
    public void CreateIdForMember_ConstructedGenericType_ReturnsTheDefinitionId()
    {
        // Arrange
        var type = typeof(GenericSample<string>);

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(type);

        // Assert
        // The compiler documents a generic type once, under its definition.
        id.ShouldBe($"T:{Ns}.GenericSample`1");
    }

    [Fact]
    public void CreateIdForMember_GenericTypeDefinition_WritesItsArityOnce()
    {
        XmlDocumentationIdBuilder.CreateIdForMember(typeof(GenericSample<>)).ShouldBe($"T:{Ns}.GenericSample`1");
    }

    [Fact]
    public void CreateIdForMember_GenericTypeNestedInGenericType_WritesEachLevelsArity()
    {
        XmlDocumentationIdBuilder.CreateIdForMember(typeof(GenericSample<int>.Inner<string>))
            .ShouldBe($"T:{Ns}.GenericSample`1.Inner`1");
    }

    [Fact]
    public void CreateIdForMember_MembersOfAConstructedGenericType_UseTheDefinitionsSignature()
    {
        // Arrange
        var type = typeof(GenericSample<string>);

        // Act
        var property = XmlDocumentationIdBuilder.CreateIdForMember(type.GetProperty(nameof(GenericSample<string>.Value))!);
        var method = XmlDocumentationIdBuilder.CreateIdForMember(type.GetMethod(nameof(GenericSample<string>.Replace))!);
        var field = XmlDocumentationIdBuilder.CreateIdForMember(type.GetField(nameof(GenericSample<string>.Fallback))!);

        // Assert
        property.ShouldBe($"P:{Ns}.GenericSample`1.Value");
        method.ShouldBe($"M:{Ns}.GenericSample`1.Replace(`0,System.Collections.Generic.List{{`0}})");
        field.ShouldBe($"F:{Ns}.GenericSample`1.Fallback");
    }

    [Fact]
    public void CreateIdForMember_GenericMethod_ReturnsCorrectId()
    {
        // Arrange
        var method = typeof(SampleType).GetMethod(nameof(SampleType.GenericMethod))!;

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(method);

        // Assert
        id.ShouldBe("M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.GenericMethod``1(``0)");
    }

    [Theory]
    [InlineData(nameof(SampleType.WithArrays), "(System.Int32[],System.String[0:,0:],System.Byte[0:,0:,0:])")]
    [InlineData(nameof(SampleType.WithRefAndOut), "(System.Int32@,System.String@)")]
    [InlineData(nameof(SampleType.WithNestedType), "(" + Ns + ".SampleType.NestedType)")]
    [InlineData(nameof(SampleType.WithGenericArguments), "(System.Collections.Generic.Dictionary{System.String,System.Collections.Generic.List{System.Int32}})")]
    public void CreateIdForMember_MethodParameters_UseDocumentationIdSyntax(string methodName, string expectedParameters)
    {
        // Arrange
        var method = typeof(SampleType).GetMethod(methodName)!;

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(method);

        // Assert
        id.ShouldBe($"M:{Ns}.SampleType.{methodName}{expectedParameters}");
    }

    [Fact]
    public void CreateIdForMember_ConversionOperator_AppendsTheReturnType()
    {
        // Arrange
        var method = typeof(SampleType).GetMethods().Single(m => m.Name == "op_Implicit");

        // Act
        var id = XmlDocumentationIdBuilder.CreateIdForMember(method);

        // Assert
        id.ShouldBe($"M:{Ns}.SampleType.op_Implicit({Ns}.SampleType)~System.Int32");
    }

    [Fact]
    public void CreateIdForMember_IndexerFieldAndEvent_ReturnTheirIds()
    {
        // Arrange
        var type = typeof(SampleType);

        // Act
        var indexer = XmlDocumentationIdBuilder.CreateIdForMember(type.GetProperty("Item")!);
        var field = XmlDocumentationIdBuilder.CreateIdForMember(type.GetField(nameof(SampleType.Counter))!);
        var @event = XmlDocumentationIdBuilder.CreateIdForMember(type.GetEvent(nameof(SampleType.Changed))!);

        // Assert
        indexer.ShouldBe($"P:{Ns}.SampleType.Item(System.Int32,System.String)");
        field.ShouldBe($"F:{Ns}.SampleType.Counter");
        @event.ShouldBe($"E:{Ns}.SampleType.Changed");
    }

    [Fact]
    public void CreateIdForMember_Constructor_ReturnsNull()
    {
        XmlDocumentationIdBuilder.CreateIdForMember(typeof(SampleType).GetConstructor(Type.EmptyTypes)!).ShouldBeNull();
    }

    [Fact]
    public void CreateIdForMember_MatchesTheCompilersIdsForTheCoreAssembly()
    {
        // Arrange
        // The compiler's own documentation file for the core library is the ground truth: every documented type,
        // method, property, field and event id in it has to be one the builder produces for some member.
        var assembly = typeof(AsyncApiOptions).Assembly;
        var xmlPath = Path.Combine(AppContext.BaseDirectory, assembly.GetName().Name + ".xml");
        var documented = XDocument.Load(xmlPath)
            .Descendants("member")
            .Select(m => m.Attribute("name")!.Value)
            .Where(id => id[0] is 'T' or 'M' or 'P' or 'F' or 'E' && !id.Contains(".#ctor") && !id.Contains(".#cctor"))
            // The regex source generator's types are file-local, so reflection sees them under mangled names.
            .Where(id => !id.Contains("System.Text.RegularExpressions.Generated."))
            .ToList();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                 BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Act
        var produced = assembly.GetTypes()
            .SelectMany(t => t.GetMembers(all).Prepend(t))
            .Select(XmlDocumentationIdBuilder.CreateIdForMember)
            .OfType<string>()
            .ToHashSet();

        // Assert
        documented.ShouldNotBeEmpty();
        documented.Where(id => !produced.Contains(id)).ShouldBeEmpty();
    }
}

public class SampleType
{
    public int Counter;

    public string SampleProperty { get; set; } = string.Empty;

    public event EventHandler? Changed;

    public int this[int index, string key] => index;

    public void SampleMethod() { }

    public void MethodWithParameters(string s, int i) { }

    public void GenericMethod<T>(T value) { }

    public void WithArrays(int[] values, string[,] grid, byte[,,] cube) { }

    public void WithRefAndOut(ref int value, out string result) => result = string.Empty;

    public void WithNestedType(NestedType nested) { }

    public void WithGenericArguments(Dictionary<string, List<int>> map) { }

    public static implicit operator int(SampleType sample) => sample.Counter;

    protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public class NestedType { }
}

public class GenericSample<T>
{
    public T? Fallback;

    public T? Value { get; set; }

    public void Replace(T value, List<T> history) { }

    public class Inner<TInner> { }
}
