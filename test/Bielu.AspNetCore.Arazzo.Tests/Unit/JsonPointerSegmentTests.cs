using Bielu.AspNetCore.Arazzo.SourceResolvers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class JsonPointerSegmentTests
{
    [Theory]
    [InlineData("lightMeasured", "lightMeasured")]
    [InlineData("foo~1bar", "foo/bar")]
    [InlineData("foo~0bar", "foo~bar")]
    [InlineData("~1~0", "/~")]
    public void TryUnescape_AcceptsWellFormedSegments(string segment, string expected)
    {
        var result = JsonPointerSegment.TryUnescape(segment, out var unescaped);

        result.ShouldBeTrue();
        unescaped.ShouldBe(expected);
    }

    [Theory]
    [InlineData("foo/bar")] // raw, unescaped '/' — not a single segment
    [InlineData("foo~2bar")] // '~' must be followed by '0' or '1'
    [InlineData("foo~")] // trailing '~' with nothing following
    public void TryUnescape_RejectsMalformedSegments(string segment)
    {
        var result = JsonPointerSegment.TryUnescape(segment, out var unescaped);

        result.ShouldBeFalse();
        unescaped.ShouldBe(string.Empty);
    }
}
