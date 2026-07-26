namespace Bielu.Arazzo.Expressions;

/// <summary>
/// Parsed form of a spec §5.9 Runtime Expression. One subtype per ABNF alternative — see
/// <see cref="RuntimeExpressionParser"/> for how these are produced and
/// <see cref="RuntimeExpressionEvaluator"/> for how they are resolved against a caller-supplied
/// <see cref="IRuntimeExpressionContext"/>.
/// </summary>
public abstract record RuntimeExpression(string Raw)
{
    public sealed record Url(string Raw) : RuntimeExpression(Raw);

    public sealed record Method(string Raw) : RuntimeExpression(Raw);

    public sealed record StatusCode(string Raw) : RuntimeExpression(Raw);

    public sealed record Self(string Raw) : RuntimeExpression(Raw);

    public sealed record Request(string Raw, RuntimeExpressionSource Source) : RuntimeExpression(Raw);

    public sealed record Response(string Raw, RuntimeExpressionSource Source) : RuntimeExpression(Raw);

    /// <summary>AsyncAPI-message-scoped source — new in spec 1.1.0.</summary>
    public sealed record Message(string Raw, RuntimeExpressionSource Source) : RuntimeExpression(Raw);

    public sealed record Inputs(string Raw, string Name, string? JsonPointer) : RuntimeExpression(Raw);

    public sealed record Outputs(string Raw, string Name, string? JsonPointer) : RuntimeExpression(Raw);

    public sealed record Steps(string Raw, string StepId, string OutputName, string? JsonPointer) : RuntimeExpression(Raw);

    /// <summary><see cref="Field"/> is "inputs" or "outputs".</summary>
    public sealed record Workflows(string Raw, string WorkflowName, string Field, string? FieldName, string? JsonPointer) : RuntimeExpression(Raw);

    /// <summary><see cref="ReferenceId"/> is an operationId or workflowId — spec notes these have no character restrictions, so it is not dot-split further.</summary>
    public sealed record SourceDescriptions(string Raw, string SourceName, string ReferenceId) : RuntimeExpression(Raw);

    /// <summary><see cref="Field"/> is one of "parameters", "successActions", "failureActions"; <see cref="Name"/> may itself contain dots per the components key regex.</summary>
    public sealed record Components(string Raw, string Field, string Name) : RuntimeExpression(Raw);
}

public enum RuntimeExpressionSourceKind
{
    Header,
    Query,
    Path,
    Body,
    Payload,
}

/// <summary>The <c>source</c> ABNF rule shared by $request, $response, and $message.</summary>
public sealed record RuntimeExpressionSource(RuntimeExpressionSourceKind Kind, string? Name, string? JsonPointer);
