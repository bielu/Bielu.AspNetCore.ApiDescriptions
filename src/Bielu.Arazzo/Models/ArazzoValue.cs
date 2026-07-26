using System.Text.Json.Nodes;
using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>
/// The <c>Any | {expression} | Selector Object</c> shape shared by Parameter.value, RequestBody.payload,
/// PayloadReplacement.value, and the outputs maps (<c>{expression} | Selector Object</c> there — Literal
/// stays unset in that position). Exactly one of the three should be set.
/// </summary>
public sealed class ArazzoValue : IArazzoSerializable
{
    /// <summary>A constant JSON value (string, number, boolean, null, object, or array).</summary>
    public JsonNode? Literal { get; set; }

    /// <summary>A runtime expression string, e.g. <c>$inputs.username</c>.</summary>
    public string? Expression { get; set; }

    public ArazzoSelector? Selector { get; set; }

    public bool IsExpression => Expression is not null;

    public bool IsSelector => Selector is not null;

    public static implicit operator ArazzoValue(string expression) => new() { Expression = expression };

    public static ArazzoValue FromLiteral(JsonNode? literal) => new() { Literal = literal };

    public void SerializeAsV1(IArazzoWriter writer)
    {
        if (Selector is not null)
        {
            Selector.SerializeAsV1(writer);
            return;
        }

        if (Expression is not null)
        {
            writer.WriteValue(Expression);
            return;
        }

        writer.WriteRaw(Literal);
    }
}
