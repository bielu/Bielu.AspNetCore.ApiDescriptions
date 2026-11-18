namespace Bielu.Arazzo.Models;

/// <summary>
/// An AsyncAPI step's <c>action</c>: <c>send</c> or <c>receive</c>. It holds any string, so a value outside the specification survives reading and writing and is
/// reported by <see cref="Validation.ArazzoValidator"/>; a plain string converts to it implicitly.
/// </summary>
public readonly record struct ArazzoStepAction
{
    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form, such as <c>"send"</c>.</param>
    public ArazzoStepAction(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>The string form, as written in the document.</summary>
    public string Value { get; }

    /// <summary>The application publishes the message.</summary>
    public static ArazzoStepAction Send { get; } = new("send");

    /// <summary>The application subscribes to the message.</summary>
    public static ArazzoStepAction Receive { get; } = new("receive");

    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form.</param>
    public static implicit operator ArazzoStepAction(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Whether this is one of the values the specification defines.</summary>
    public bool IsKnown => Value == "send" || Value == "receive";
}

/// <summary>
/// A Parameter Object's <c>in</c>: where the parameter goes. It holds any string, so a value outside the specification survives reading and writing and is
/// reported by <see cref="Validation.ArazzoValidator"/>; a plain string converts to it implicitly.
/// </summary>
public readonly record struct ArazzoParameterLocation
{
    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form, such as <c>"path"</c>.</param>
    public ArazzoParameterLocation(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>The string form, as written in the document.</summary>
    public string Value { get; }

    /// <summary>A path parameter.</summary>
    public static ArazzoParameterLocation Path { get; } = new("path");

    /// <summary>A query parameter.</summary>
    public static ArazzoParameterLocation Query { get; } = new("query");

    /// <summary>The whole query string (Arazzo 1.1, OpenAPI 3.2).</summary>
    public static ArazzoParameterLocation QueryString { get; } = new("querystring");

    /// <summary>A header.</summary>
    public static ArazzoParameterLocation Header { get; } = new("header");

    /// <summary>A cookie.</summary>
    public static ArazzoParameterLocation Cookie { get; } = new("cookie");

    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form.</param>
    public static implicit operator ArazzoParameterLocation(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Whether this is one of the values the specification defines.</summary>
    public bool IsKnown => Value == "path" || Value == "query" || Value == "querystring" || Value == "header" || Value == "cookie";
}

/// <summary>
/// A Source Description Object's <c>type</c>. It holds any string, so a value outside the specification survives reading and writing and is
/// reported by <see cref="Validation.ArazzoValidator"/>; a plain string converts to it implicitly.
/// </summary>
public readonly record struct ArazzoSourceDescriptionType
{
    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form, such as <c>"openapi"</c>.</param>
    public ArazzoSourceDescriptionType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>The string form, as written in the document.</summary>
    public string Value { get; }

    /// <summary>An OpenAPI description.</summary>
    public static ArazzoSourceDescriptionType OpenApi { get; } = new("openapi");

    /// <summary>An AsyncAPI description (Arazzo 1.1).</summary>
    public static ArazzoSourceDescriptionType AsyncApi { get; } = new("asyncapi");

    /// <summary>Another Arazzo description.</summary>
    public static ArazzoSourceDescriptionType Arazzo { get; } = new("arazzo");

    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form.</param>
    public static implicit operator ArazzoSourceDescriptionType(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Whether this is one of the values the specification defines.</summary>
    public bool IsKnown => Value == "openapi" || Value == "asyncapi" || Value == "arazzo";
}

/// <summary>
/// A Success Action Object's <c>type</c>. It holds any string, so a value outside the specification survives reading and writing and is
/// reported by <see cref="Validation.ArazzoValidator"/>; a plain string converts to it implicitly.
/// </summary>
public readonly record struct ArazzoSuccessActionType
{
    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form, such as <c>"end"</c>.</param>
    public ArazzoSuccessActionType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>The string form, as written in the document.</summary>
    public string Value { get; }

    /// <summary>Ends the workflow.</summary>
    public static ArazzoSuccessActionType End { get; } = new("end");

    /// <summary>Continues at another workflow or step.</summary>
    public static ArazzoSuccessActionType Goto { get; } = new("goto");

    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form.</param>
    public static implicit operator ArazzoSuccessActionType(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Whether this is one of the values the specification defines.</summary>
    public bool IsKnown => Value == "end" || Value == "goto";
}

/// <summary>
/// A Failure Action Object's <c>type</c>. It holds any string, so a value outside the specification survives reading and writing and is
/// reported by <see cref="Validation.ArazzoValidator"/>; a plain string converts to it implicitly.
/// </summary>
public readonly record struct ArazzoFailureActionType
{
    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form, such as <c>"end"</c>.</param>
    public ArazzoFailureActionType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>The string form, as written in the document.</summary>
    public string Value { get; }

    /// <summary>Ends workflow execution.</summary>
    public static ArazzoFailureActionType End { get; } = new("end");

    /// <summary>Retries the failed operation.</summary>
    public static ArazzoFailureActionType Retry { get; } = new("retry");

    /// <summary>Continues execution at another workflow or step.</summary>
    public static ArazzoFailureActionType Goto { get; } = new("goto");

    /// <summary>Creates a value from its string form.</summary>
    /// <param name="value">The string form.</param>
    public static implicit operator ArazzoFailureActionType(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Whether this is one of the values the specification defines.</summary>
    public bool IsKnown => Value == "end" || Value == "retry" || Value == "goto";
}
