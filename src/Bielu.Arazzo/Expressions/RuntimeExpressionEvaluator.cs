using System.Text.Json.Nodes;

namespace Bielu.Arazzo.Expressions;

/// <summary>Resolves a parsed <see cref="RuntimeExpression"/> against an <see cref="IRuntimeExpressionContext"/>.</summary>
public static class RuntimeExpressionEvaluator
{
    /// <summary>Evaluates the given expression against the given context and returns the resolved JSON value, or null if the referenced value is absent.</summary>
    public static JsonNode? Evaluate(RuntimeExpression expression, IRuntimeExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(context);
        return expression switch
        {
            RuntimeExpression.Url => JsonValue.Create(context.Url),
            RuntimeExpression.Method => JsonValue.Create(context.Method),
            RuntimeExpression.StatusCode => context.StatusCode is { } code ? JsonValue.Create(code) : null,
            RuntimeExpression.Self => JsonValue.Create(context.Self),
            RuntimeExpression.Request r => context.GetRequestValue(r.Source),
            RuntimeExpression.Response r => context.GetResponseValue(r.Source),
            RuntimeExpression.Message m => context.GetMessageValue(m.Source),
            RuntimeExpression.Inputs i => ArazzoJsonPointerHelper.Evaluate(i.JsonPointer, context.GetInput(i.Name)),
            RuntimeExpression.Outputs o => ArazzoJsonPointerHelper.Evaluate(o.JsonPointer, context.GetOutput(o.Name)),
            RuntimeExpression.Steps s => ArazzoJsonPointerHelper.Evaluate(s.JsonPointer, context.GetStepOutput(s.StepId, s.OutputName)),
            RuntimeExpression.Workflows w => ArazzoJsonPointerHelper.Evaluate(w.JsonPointer, context.GetWorkflowField(w.WorkflowName, w.Field, w.FieldName)),
            RuntimeExpression.SourceDescriptions sd => context.GetSourceDescriptionReference(sd.SourceName, sd.ReferenceId),
            RuntimeExpression.Components c => context.GetComponent(c.Field, c.Name),
            _ => throw new NotSupportedException($"Unsupported runtime expression kind: {expression.GetType().Name}"),
        };
    }
}
