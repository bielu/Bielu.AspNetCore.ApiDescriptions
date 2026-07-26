using System.Text.Json.Nodes;
using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Which variant of the <c>Any | {expression} | Selector Object</c> shape an <see cref="ArazzoValue"/> holds.</summary>
public enum ArazzoValueKind
{
    /// <summary>A constant JSON value; see <see cref="ArazzoValue.Literal"/>.</summary>
    Literal,

    /// <summary>A runtime expression string; see <see cref="ArazzoValue.Expression"/>.</summary>
    Expression,

    /// <summary>A Selector Object; see <see cref="ArazzoValue.Selector"/>.</summary>
    Selector,
}

/// <summary>
/// The <c>Any | {expression} | Selector Object</c> shape shared by Parameter.value, RequestBody.payload,
/// PayloadReplacement.value, and the outputs maps (<c>{expression} | Selector Object</c> there — Literal
/// stays unset in that position). Exactly one variant — <see cref="ArazzoValueKind.Literal"/>,
/// <see cref="ArazzoValueKind.Expression"/>, or <see cref="ArazzoValueKind.Selector"/> — is ever active,
/// as reflected by <see cref="Kind"/>; construct instances via <see cref="FromLiteral"/>,
/// <see cref="FromExpression"/>, or <see cref="FromSelector"/>.
/// </summary>
public sealed class ArazzoValue : IArazzoSerializable
{
    private readonly JsonNode? _literal;
    private readonly string? _expression;
    private readonly ArazzoSelector? _selector;

    private ArazzoValue(ArazzoValueKind kind, JsonNode? literal, string? expression, ArazzoSelector? selector)
    {
        Kind = kind;
        _literal = literal;
        _expression = expression;
        _selector = selector;
    }

    /// <summary>Which variant is active.</summary>
    public ArazzoValueKind Kind { get; }

    /// <summary>The constant JSON value (string, number, boolean, null, object, or array) when <see cref="Kind"/> is <see cref="ArazzoValueKind.Literal"/>; otherwise null. A JSON <c>null</c> literal is preserved and distinguishable from an unset value via <see cref="Kind"/>.</summary>
    public JsonNode? Literal => Kind == ArazzoValueKind.Literal ? _literal : null;

    /// <summary>The runtime expression string, e.g. <c>$inputs.username</c>, when <see cref="Kind"/> is <see cref="ArazzoValueKind.Expression"/>; otherwise null.</summary>
    public string? Expression => Kind == ArazzoValueKind.Expression ? _expression : null;

    /// <summary>The Selector Object when <see cref="Kind"/> is <see cref="ArazzoValueKind.Selector"/>; otherwise null.</summary>
    public ArazzoSelector? Selector => Kind == ArazzoValueKind.Selector ? _selector : null;

    /// <summary><c>true</c> when <see cref="Kind"/> is <see cref="ArazzoValueKind.Expression"/>.</summary>
    public bool IsExpression => Kind == ArazzoValueKind.Expression;

    /// <summary><c>true</c> when <see cref="Kind"/> is <see cref="ArazzoValueKind.Selector"/>.</summary>
    public bool IsSelector => Kind == ArazzoValueKind.Selector;

    /// <summary><c>true</c> when <see cref="Kind"/> is <see cref="ArazzoValueKind.Literal"/>.</summary>
    public bool IsLiteral => Kind == ArazzoValueKind.Literal;

    /// <summary>Creates an expression-variant value from a runtime expression string.</summary>
    /// <param name="expression">The runtime expression.</param>
    /// <returns>An expression-variant Arazzo value.</returns>
    public static implicit operator ArazzoValue(string expression) => FromExpression(expression);

    /// <summary>Creates a literal-variant value. <paramref name="literal"/> may itself be a JSON <c>null</c>, which is preserved as a genuine literal rather than treated as unset.</summary>
    public static ArazzoValue FromLiteral(JsonNode? literal) => new(ArazzoValueKind.Literal, literal, null, null);

    /// <summary>Creates an expression-variant value.</summary>
    public static ArazzoValue FromExpression(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new ArazzoValue(ArazzoValueKind.Expression, null, expression, null);
    }

    /// <summary>Creates a selector-variant value.</summary>
    public static ArazzoValue FromSelector(ArazzoSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new ArazzoValue(ArazzoValueKind.Selector, null, null, selector);
    }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (Kind)
        {
            case ArazzoValueKind.Selector:
                if (_selector is null)
                {
                    throw new InvalidOperationException("A selector value must contain a selector.");
                }

                _selector.SerializeAsV1(writer);
                break;
            case ArazzoValueKind.Expression:
                writer.WriteValue(_expression);
                break;
            default:
                writer.WriteRaw(_literal);
                break;
        }
    }
}
