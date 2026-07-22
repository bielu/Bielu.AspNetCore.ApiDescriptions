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
            Arg.Any<Func<object, Exception, string>>());
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
            var method = typeof(SampleType).GetMethod(nameof(SampleType.MethodWithParameters));
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
}
