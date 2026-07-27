using Bielu.AspNetCore.Arazzo;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoIdentifierTests
{
    [Theory]
    [InlineData("events")]
    [InlineData("events_v2")]
    [InlineData("events-v2")]
    [InlineData("Events123")]
    public void Validate_AcceptsGrammarConformingIdentifiers(string value)
    {
        Should.NotThrow(() => ArazzoIdentifier.Validate(value, "value"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("events.v2")]
    [InlineData("events/v2")]
    [InlineData("events v2")]
    [InlineData("évents")]
    public void Validate_RejectsNonConformingIdentifiers(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => ArazzoIdentifier.Validate(value!, "value"));
        exception.ParamName.ShouldBe("value");
    }
}
