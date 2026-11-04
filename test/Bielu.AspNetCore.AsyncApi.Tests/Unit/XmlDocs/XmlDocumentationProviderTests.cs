using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs;

public class XmlDocumentationProviderTests
{
    private readonly ILogger<XmlDocumentationProvider> _logger = Substitute.For<ILogger<XmlDocumentationProvider>>();
    private readonly XmlDocumentationProvider _provider;

    public XmlDocumentationProviderTests()
    {
        _provider = new XmlDocumentationProvider(_logger);
    }

    [Fact]
    public void Load_MissingFile_LogsWarning()
    {
        // Arrange
        var filePath = "non-existent.xml";

        // Act
        _provider.Load(filePath);

        // Assert
        _logger.ReceivedWithAnyArgs(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Load_ValidFile_PopulatesCache()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        var xml = @"
<doc>
    <members>
        <member name=""T:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType"">
            <summary>Sample summary</summary>
            <remarks>Sample remarks</remarks>
        </member>
    </members>
</doc>";
        File.WriteAllText(filePath, xml);

        try
        {
            // Act
            _provider.Load(filePath);
            var doc = _provider.GetDocumentation(typeof(SampleType));

            // Assert
            doc.ShouldNotBeNull();
            doc.Summary.ShouldBe("Sample summary");
            doc.Remarks.ShouldBe("Sample remarks");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_MethodWithParameters_PopulatesParameters()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        var xml = @"
<doc>
    <members>
        <member name=""M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.MethodWithParameters(System.String,System.Int32)"">
            <param name=""s"">String parameter</param>
            <param name=""i"">Int parameter</param>
        </member>
    </members>
</doc>";
        File.WriteAllText(filePath, xml);

        try
        {
            // Act
            _provider.Load(filePath);
            var method = typeof(SampleType).GetMethod(nameof(SampleType.MethodWithParameters))!;
            var doc = _provider.GetDocumentation(method);

            // Assert
            doc.ShouldNotBeNull();
            doc.Parameters.ShouldNotBeNull();
            doc.Parameters["s"].ShouldBe("String parameter");
            doc.Parameters["i"].ShouldBe("Int parameter");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(@"Arrives as <see cref=""T:My.Events.CaseUpdated""/>.", "Arrives as CaseUpdated.")]
    [InlineData(@"A list: <see cref=""T:System.Collections.Generic.List`1""/>.", "A list: List.")]
    [InlineData(@"Call <see cref=""M:My.Service.Send(System.String,System.Int32)""/>.", "Call Service.Send.")]
    [InlineData(@"Read <see cref=""P:My.Events.CaseUpdated.Owner""/>.", "Read CaseUpdated.Owner.")]
    [InlineData(@"Uses <see cref=""T:My.Events.CaseUpdated"">the update event</see>.", "Uses the update event.")]
    [InlineData(@"Returns <see langword=""null""/> when absent.", "Returns null when absent.")]
    [InlineData(@"Uses <paramref name=""value""/> and <typeparamref name=""T""/>.", "Uses value and T.")]
    [InlineData(@"See <see href=""https://example.com""/>.", "See https://example.com.")]
    [InlineData(@"See <see href=""https://example.com"">the docs</see>.", "See the docs.")]
    [InlineData(@"<see cref=""T:My.A""/> <see cref=""T:My.B""/>", "A B")]
    [InlineData(@"Either <c>OPEN</c> or <c>CLOSED</c>.", "Either OPEN or CLOSED.")]
    public void Load_SummaryWithInlineTags_RendersTagsAsText(string summaryXml, string expected)
    {
        // Arrange
        var filePath = WriteSampleTypeDocs($"<summary>{summaryXml}</summary>");

        try
        {
            // Act
            _provider.Load(filePath);
            var doc = _provider.GetDocumentation(typeof(SampleType));

            // Assert
            doc.ShouldNotBeNull();
            doc.Summary.ShouldBe(expected);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_MultilineSummary_JoinsLinesWithSingleSpaces()
    {
        // Arrange
        var filePath = WriteSampleTypeDocs("""
            <summary>
                        Pushed to connected clients when a case is opened. Carries only the case's own fields; clients
                        refetch the Case table for the linked disruption.
                        </summary>
            """);

        try
        {
            // Act
            _provider.Load(filePath);
            var doc = _provider.GetDocumentation(typeof(SampleType));

            // Assert
            doc.ShouldNotBeNull();
            doc.Summary.ShouldBe(
                "Pushed to connected clients when a case is opened. Carries only the case's own fields; clients refetch the Case table for the linked disruption.");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_RemarksWithParagraphs_SeparatesParagraphsWithBlankLine()
    {
        // Arrange
        var filePath = WriteSampleTypeDocs("""
            <remarks>
                        First paragraph,
                        still the first.
                        <para>Second paragraph.</para>
                        <para>Third
                        paragraph.</para>
                        </remarks>
            """);

        try
        {
            // Act
            _provider.Load(filePath);
            var doc = _provider.GetDocumentation(typeof(SampleType));

            // Assert
            doc.ShouldNotBeNull();
            doc.Remarks.ShouldBe("First paragraph, still the first.\n\nSecond paragraph.\n\nThird paragraph.");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("First line<br/>second line.", "First line\nsecond line.")]
    [InlineData(
        "States:\n<list type=\"bullet\">\n  <item>OPEN</item>\n  <item>CLOSED, with\n  a reason</item>\n</list>\nThen more.",
        "States:\n\n- OPEN\n- CLOSED, with a reason\n\nThen more.")]
    [InlineData(
        "Example:\n<code>\nvar x = 1;\n    Send(x);\n</code>",
        "Example:\n\nvar x = 1;\n    Send(x);")]
    public void Load_RemarksWithBlockTags_RendersLineStructure(string remarksXml, string expected)
    {
        // Arrange
        var filePath = WriteSampleTypeDocs($"<remarks>{remarksXml}</remarks>");

        try
        {
            // Act
            _provider.Load(filePath);
            var doc = _provider.GetDocumentation(typeof(SampleType));

            // Assert
            doc.ShouldNotBeNull();
            doc.Remarks.ShouldBe(expected);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_EmptySummary_ReturnsNullSummary()
    {
        // Arrange
        var filePath = WriteSampleTypeDocs("<summary>\n   \n</summary>");

        try
        {
            // Act
            _provider.Load(filePath);
            var doc = _provider.GetDocumentation(typeof(SampleType));

            // Assert
            doc.ShouldNotBeNull();
            doc.Summary.ShouldBeNull();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_ParamWithCrefAndLineBreaks_RendersParamAsSingleLine()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        File.WriteAllText(filePath, """
            <doc>
                <members>
                    <member name="M:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType.MethodWithParameters(System.String,System.Int32)">
                        <param name="s">The state; never closed
                        (a close is <see cref="T:My.Events.CaseClosed"/>).</param>
                    </member>
                </members>
            </doc>
            """);

        try
        {
            // Act
            _provider.Load(filePath);
            var method = typeof(SampleType).GetMethod(nameof(SampleType.MethodWithParameters))!;
            var doc = _provider.GetDocumentation(method);

            // Assert
            doc.ShouldNotBeNull();
            doc.Parameters.ShouldNotBeNull();
            doc.Parameters["s"].ShouldBe("The state; never closed (a close is CaseClosed).");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string WriteSampleTypeDocs(string memberXml)
    {
        var filePath = Path.GetTempFileName();
        File.WriteAllText(filePath, $"""
            <doc>
                <members>
                    <member name="T:Bielu.AspNetCore.AsyncApi.Tests.Unit.XmlDocs.SampleType">
                        {memberXml}
                    </member>
                </members>
            </doc>
            """);
        return filePath;
    }
}
