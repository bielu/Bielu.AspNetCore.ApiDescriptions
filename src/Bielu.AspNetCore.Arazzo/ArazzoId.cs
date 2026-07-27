using System.Text.Json;

namespace Bielu.AspNetCore.Arazzo;

/// <summary>
/// The convention mapping a marker type to the <c>workflowId</c>/<c>stepId</c> it identifies, used by the
/// generic overloads on the fluent builders (<c>AddWorkflow&lt;T&gt;</c>, <c>Step&lt;T&gt;</c>,
/// <c>DependsOn&lt;T&gt;</c>, <c>Workflow&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// The type's name is camel-cased, so <c>MeasureAndAlert</c> becomes <c>measureAndAlert</c> — matching both
/// the casing the Arazzo specification's own examples use and the identifier-strict grammar in §5.9
/// (<c>[A-Za-z0-9_-]+</c>), while letting the marker type keep idiomatic C# PascalCase. Because the string
/// and generic overloads both funnel through here, <c>AddWorkflow("measureAndAlert", …)</c> and
/// <c>DependsOn&lt;MeasureAndAlert&gt;()</c> refer to the same workflow.
/// </remarks>
public static class ArazzoId
{
    /// <summary>Returns the id <paramref name="type"/> identifies.</summary>
    /// <param name="type">The marker type naming a workflow or step.</param>
    public static string FromType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var id = JsonNamingPolicy.CamelCase.ConvertName(type.Name);
        try
        {
            ArazzoIdentifier.Validate(id, nameof(type));
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException(
                $"Marker type '{type}' camel-cases to the id '{id}', which is not a valid Arazzo identifier " +
                "('^[A-Za-z0-9_-]+$'). This typically happens with generic marker types (the backtick/arity " +
                "suffix isn't valid) — use a non-generic marker type or the string-id overload instead.",
                nameof(type), ex);
        }

        return id;
    }

    /// <summary>Returns the id <typeparamref name="T"/> identifies.</summary>
    /// <typeparam name="T">The marker type naming a workflow or step.</typeparam>
    public static string FromType<T>() => FromType(typeof(T));
}
