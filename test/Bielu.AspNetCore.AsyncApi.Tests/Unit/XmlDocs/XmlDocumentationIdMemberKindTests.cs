using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs;

/// <summary>
/// <see cref="XmlDocumentationIdBuilder"/> for member kinds and parameter shapes beyond plain methods and types.
/// The expected IDs are the ones the C# compiler writes into XML documentation files, so a mismatch means the
/// documentation of that member could never be found.
/// </summary>
public class XmlDocumentationIdMemberKindTests
{
    private const string Sample = "Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.IdSample";
    private const BindingFlags Everything = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static string? IdOf(MemberInfo member) => XmlDocumentationIdBuilder.CreateIdForMember(member);

    [Fact]
    public void CreateIdForMember_FieldAndEvent_UseTheirPrefixes()
    {
        // Act & Assert
        IdOf(typeof(IdSample).GetField(nameof(IdSample.Count))!).ShouldBe($"F:{Sample}.Count");
        IdOf(typeof(IdSample).GetEvent(nameof(IdSample.Changed))!).ShouldBe($"E:{Sample}.Changed");
    }

    [Fact]
    public void CreateIdForMember_Indexer_ListsItsParameters()
    {
        // Act & Assert
        IdOf(typeof(IdSample).GetProperty("Item")!).ShouldBe($"P:{Sample}.Item(System.Int32,System.String)");
    }

    [Fact]
    public void CreateIdForMember_ArrayParameters_MatchTheCompilerForm()
    {
        // Act & Assert: multi-dimensional arrays carry a "0:" lower bound per dimension.
        IdOf(typeof(IdSample).GetMethod(nameof(IdSample.Arrays))!)
            .ShouldBe($"M:{Sample}.Arrays(System.Int32[],System.String[][],System.Int32[0:,0:],System.Int32[0:,0:,0:])");
    }

    [Fact]
    public void CreateIdForMember_ByRefParameters_UseAtSign()
    {
        // Act & Assert
        IdOf(typeof(IdSample).GetMethod(nameof(IdSample.References))!).ShouldBe($"M:{Sample}.References(System.Int32@,System.String@)");
    }

    [Fact]
    public void CreateIdForMember_GenericMethodWithArrayOfTypeParameter_MatchesTheCompilerForm()
    {
        // Act & Assert
        IdOf(typeof(IdSample).GetMethod(nameof(IdSample.Generic))!)
            .ShouldBe($"M:{Sample}.Generic``1(``0[0:,0:],System.Collections.Generic.List{{``0}})");
    }

    [Fact]
    public void CreateIdForMember_ConversionOperators_AppendTheReturnType()
    {
        // Arrange
        var operators = typeof(IdSample).GetMethods(Everything).Where(method => method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal)).ToList();

        // Act & Assert
        IdOf(operators.Single(method => method.Name == "op_Implicit")).ShouldBe($"M:{Sample}.op_Implicit({Sample})~System.Int32");
        IdOf(operators.Single(method => method.Name == "op_Explicit")).ShouldBe($"M:{Sample}.op_Explicit(System.Int64)~{Sample}");
    }

    [Fact]
    public void CreateIdForMember_GenericTypeDefinitionAndItsMethod_UseArityAndPosition()
    {
        // Act & Assert
        IdOf(typeof(IdGeneric<>)).ShouldBe("T:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.IdGeneric`1");
        IdOf(typeof(IdGeneric<>).GetMethod(nameof(IdGeneric<int>.Echo))!)
            .ShouldBe("M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.IdGeneric`1.Echo(`0)");
    }

    [Fact]
    public void CreateIdForMember_Constructor_ReturnsNull()
    {
        // Act & Assert: constructors are never looked up, so no ID is produced.
        IdOf(typeof(IdSample).GetConstructors()[0]).ShouldBeNull();
    }
}

#pragma warning disable CS0067 // The event is only reflected over.
#pragma warning disable CA1822, IDE0060 // Members exist only to be named.
public class IdSample
{
    public int Count;

    public event EventHandler? Changed;

    public string this[int index, string key] => string.Empty;

    public void Arrays(int[] single, string[][] jagged, int[,] grid, int[,,] cube) { }

    public void References(ref int value, out string result) => result = string.Empty;

    public void Generic<T>(T[,] values, List<T> list) { }

    public static implicit operator int(IdSample sample) => 0;

    public static explicit operator IdSample(long value) => new();
}

public class IdGeneric<T>
{
    public T Echo(T value) => value;
}
#pragma warning restore CA1822, IDE0060
#pragma warning restore CS0067
