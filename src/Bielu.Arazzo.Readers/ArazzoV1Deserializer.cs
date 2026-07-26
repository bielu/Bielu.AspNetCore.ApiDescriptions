using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.Readers;

/// <summary>
/// Walks a unified <see cref="JsonNode"/> tree — produced either directly from JSON text or converted from
/// YAML by <see cref="YamlToJsonNodeConverter"/> — into an <see cref="ArazzoDocument"/>, collecting
/// diagnostics rather than throwing. One deserializer serves both source formats.
/// </summary>
internal static class ArazzoV1Deserializer
{
    public static ArazzoDocument Deserialize(JsonNode? root, ParsingContext ctx)
    {
        if (root is not JsonObject obj)
        {
            ctx.Error("/", "Document root must be an object.");
            return new ArazzoDocument
            {
                Arazzo = string.Empty,
                Info = new ArazzoInfo { Title = string.Empty, Version = string.Empty },
                SourceDescriptions = new List<ArazzoSourceDescription>(),
                Workflows = new List<ArazzoWorkflow>(),
            };
        }

        var known = new HashSet<string> { "arazzo", "$self", "info", "sourceDescriptions", "workflows", "components" };

        return new ArazzoDocument
        {
            Arazzo = ReadRequiredString(obj, "arazzo", "/arazzo", ctx),
            Self = ReadOptionalString(obj, "$self"),
            Info = ReadInfo(Get(obj, "info"), "/info", ctx),
            SourceDescriptions = ReadRequiredArray(Get(obj, "sourceDescriptions"), "/sourceDescriptions", ctx, ReadSourceDescription),
            Workflows = ReadRequiredArray(Get(obj, "workflows"), "/workflows", ctx, ReadWorkflow),
            Components = ReadOptionalObject(obj, "components", "/components", ctx) is { } c ? ReadComponents(c, "/components", ctx) : null,
            Extensions = ReadExtensions(obj, "/", known, ctx),
        };
    }

    private static ArazzoInfo ReadInfo(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Missing required 'info' object.");
            return new ArazzoInfo { Title = string.Empty, Version = string.Empty };
        }

        var known = new HashSet<string> { "title", "summary", "description", "version" };
        return new ArazzoInfo
        {
            Title = ReadRequiredString(obj, "title", $"{path}/title", ctx),
            Summary = ReadOptionalString(obj, "summary"),
            Description = ReadOptionalString(obj, "description"),
            Version = ReadRequiredString(obj, "version", $"{path}/version", ctx),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoSourceDescription ReadSourceDescription(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Source description must be an object.");
            return new ArazzoSourceDescription { Name = string.Empty, Url = string.Empty };
        }

        var known = new HashSet<string> { "name", "url", "type" };
        return new ArazzoSourceDescription
        {
            Name = ReadRequiredString(obj, "name", $"{path}/name", ctx),
            Url = ReadRequiredString(obj, "url", $"{path}/url", ctx),
            Type = ReadOptionalString(obj, "type"),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoWorkflow ReadWorkflow(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Workflow must be an object.");
            return new ArazzoWorkflow { WorkflowId = string.Empty, Steps = new List<ArazzoStep>() };
        }

        var known = new HashSet<string>
        {
            "workflowId", "summary", "description", "inputs", "dependsOn", "steps",
            "successActions", "failureActions", "outputs", "parameters",
        };

        return new ArazzoWorkflow
        {
            WorkflowId = ReadRequiredString(obj, "workflowId", $"{path}/workflowId", ctx),
            Summary = ReadOptionalString(obj, "summary"),
            Description = ReadOptionalString(obj, "description"),
            Inputs = Get(obj, "inputs")?.DeepClone(),
            DependsOn = ReadStringArray(Get(obj, "dependsOn")),
            Steps = ReadRequiredArray(Get(obj, "steps"), $"{path}/steps", ctx, ReadStep),
            SuccessActions = ReadArray(Get(obj, "successActions"), $"{path}/successActions", ctx, ReadSuccessActionReferenceable),
            FailureActions = ReadArray(Get(obj, "failureActions"), $"{path}/failureActions", ctx, ReadFailureActionReferenceable),
            Outputs = ReadValueMap(Get(obj, "outputs")),
            Parameters = ReadArray(Get(obj, "parameters"), $"{path}/parameters", ctx, ReadParameterReferenceable),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoStep ReadStep(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Step must be an object.");
            return new ArazzoStep { StepId = string.Empty };
        }

        var known = new HashSet<string>
        {
            "description", "stepId", "operationId", "operationPath", "channelPath", "workflowId",
            "parameters", "requestBody", "successCriteria", "onSuccess", "onFailure", "outputs",
            "timeout", "correlationId", "action", "dependsOn",
        };

        return new ArazzoStep
        {
            Description = ReadOptionalString(obj, "description"),
            StepId = ReadRequiredString(obj, "stepId", $"{path}/stepId", ctx),
            OperationId = ReadOptionalString(obj, "operationId"),
            OperationPath = ReadOptionalString(obj, "operationPath"),
            ChannelPath = ReadOptionalString(obj, "channelPath"),
            WorkflowId = ReadOptionalString(obj, "workflowId"),
            Parameters = ReadArray(Get(obj, "parameters"), $"{path}/parameters", ctx, ReadParameterReferenceable),
            RequestBody = ReadOptionalObject(obj, "requestBody", $"{path}/requestBody", ctx) is { } rb ? ReadRequestBody(rb, $"{path}/requestBody", ctx) : null,
            SuccessCriteria = ReadArray(Get(obj, "successCriteria"), $"{path}/successCriteria", ctx, ReadCriterion),
            OnSuccess = ReadArray(Get(obj, "onSuccess"), $"{path}/onSuccess", ctx, ReadSuccessActionReferenceable),
            OnFailure = ReadArray(Get(obj, "onFailure"), $"{path}/onFailure", ctx, ReadFailureActionReferenceable),
            Outputs = ReadValueMap(Get(obj, "outputs")),
            Timeout = ReadOptionalInt(obj, "timeout", $"{path}/timeout", ctx),
            CorrelationId = ReadOptionalString(obj, "correlationId"),
            Action = ReadOptionalString(obj, "action"),
            DependsOn = ReadStringArray(Get(obj, "dependsOn")),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoRequestBody ReadRequestBody(JsonObject obj, string path, ParsingContext ctx)
    {
        var known = new HashSet<string> { "contentType", "payload", "replacements" };
        return new ArazzoRequestBody
        {
            ContentType = ReadOptionalString(obj, "contentType"),
            Payload = Get(obj, "payload") is { } p ? ReadValue(p) : null,
            Replacements = ReadArray(Get(obj, "replacements"), $"{path}/replacements", ctx, ReadPayloadReplacement),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoPayloadReplacement ReadPayloadReplacement(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Payload replacement must be an object.");
            return new ArazzoPayloadReplacement { Target = string.Empty, Value = ArazzoValue.FromLiteral(null) };
        }

        var known = new HashSet<string> { "target", "targetSelectorType", "value" };
        return new ArazzoPayloadReplacement
        {
            Target = ReadRequiredString(obj, "target", $"{path}/target", ctx),
            TargetSelectorType = ReadSelectorType(Get(obj, "targetSelectorType")),
            Value = ReadValue(Get(obj, "value")),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoCriterion ReadCriterion(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Criterion must be an object.");
            return new ArazzoCriterion { Condition = string.Empty };
        }

        var known = new HashSet<string> { "context", "condition", "type" };
        return new ArazzoCriterion
        {
            Context = ReadOptionalString(obj, "context"),
            Condition = ReadRequiredString(obj, "condition", $"{path}/condition", ctx),
            Type = ReadSelectorType(Get(obj, "type")),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoParameter ReadParameter(JsonObject obj, string path, ParsingContext ctx)
    {
        var known = new HashSet<string> { "name", "in", "value" };
        return new ArazzoParameter
        {
            Name = ReadRequiredString(obj, "name", $"{path}/name", ctx),
            In = ReadOptionalString(obj, "in"),
            Value = ReadValue(Get(obj, "value")),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoSuccessAction ReadSuccessAction(JsonObject obj, string path, ParsingContext ctx)
    {
        var known = new HashSet<string> { "name", "type", "workflowId", "stepId", "criteria" };
        return new ArazzoSuccessAction
        {
            Name = ReadRequiredString(obj, "name", $"{path}/name", ctx),
            Type = ReadRequiredString(obj, "type", $"{path}/type", ctx),
            WorkflowId = ReadOptionalString(obj, "workflowId"),
            StepId = ReadOptionalString(obj, "stepId"),
            Criteria = ReadArray(Get(obj, "criteria"), $"{path}/criteria", ctx, ReadCriterion),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoFailureAction ReadFailureAction(JsonObject obj, string path, ParsingContext ctx)
    {
        var known = new HashSet<string> { "name", "type", "workflowId", "stepId", "parameters", "retryAfter", "retryLimit", "criteria" };
        return new ArazzoFailureAction
        {
            Name = ReadRequiredString(obj, "name", $"{path}/name", ctx),
            Type = ReadRequiredString(obj, "type", $"{path}/type", ctx),
            WorkflowId = ReadOptionalString(obj, "workflowId"),
            StepId = ReadOptionalString(obj, "stepId"),
            Parameters = ReadArray(Get(obj, "parameters"), $"{path}/parameters", ctx, ReadParameterReferenceable),
            RetryAfter = ReadOptionalDouble(obj, "retryAfter"),
            RetryLimit = ReadOptionalInt(obj, "retryLimit", $"{path}/retryLimit", ctx),
            Criteria = ReadArray(Get(obj, "criteria"), $"{path}/criteria", ctx, ReadCriterion),
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoComponents ReadComponents(JsonObject obj, string path, ParsingContext ctx)
    {
        var known = new HashSet<string> { "inputs", "parameters", "successActions", "failureActions" };

        Dictionary<string, JsonNode?>? inputs = null;
        if (ReadOptionalObject(obj, "inputs", $"{path}/inputs", ctx) is { } inputsObj)
        {
            inputs = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            foreach (var (key, value) in inputsObj)
            {
                inputs[key] = value?.DeepClone();
            }
        }

        Dictionary<string, ArazzoParameter>? parameters = null;
        if (ReadOptionalObject(obj, "parameters", $"{path}/parameters", ctx) is { } paramsObj)
        {
            parameters = new Dictionary<string, ArazzoParameter>(StringComparer.Ordinal);
            foreach (var (key, value) in paramsObj)
            {
                if (value is JsonObject po)
                {
                    parameters[key] = ReadParameter(po, $"{path}/parameters/{key}", ctx);
                }
                else
                {
                    ctx.Error($"{path}/parameters/{key}", "Component parameter must be an object.");
                }
            }
        }

        Dictionary<string, ArazzoSuccessAction>? successActions = null;
        if (ReadOptionalObject(obj, "successActions", $"{path}/successActions", ctx) is { } saObj)
        {
            successActions = new Dictionary<string, ArazzoSuccessAction>(StringComparer.Ordinal);
            foreach (var (key, value) in saObj)
            {
                if (value is JsonObject so)
                {
                    successActions[key] = ReadSuccessAction(so, $"{path}/successActions/{key}", ctx);
                }
                else
                {
                    ctx.Error($"{path}/successActions/{key}", "Component success action must be an object.");
                }
            }
        }

        Dictionary<string, ArazzoFailureAction>? failureActions = null;
        if (ReadOptionalObject(obj, "failureActions", $"{path}/failureActions", ctx) is { } faObj)
        {
            failureActions = new Dictionary<string, ArazzoFailureAction>(StringComparer.Ordinal);
            foreach (var (key, value) in faObj)
            {
                if (value is JsonObject fo)
                {
                    failureActions[key] = ReadFailureAction(fo, $"{path}/failureActions/{key}", ctx);
                }
                else
                {
                    ctx.Error($"{path}/failureActions/{key}", "Component failure action must be an object.");
                }
            }
        }

        return new ArazzoComponents
        {
            Inputs = inputs,
            Parameters = parameters,
            SuccessActions = successActions,
            FailureActions = failureActions,
            Extensions = ReadExtensions(obj, path, known, ctx),
        };
    }

    private static ArazzoReferenceable<ArazzoParameter> ReadParameterReferenceable(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Parameter (or reference) must be an object.");
            return ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = string.Empty, Value = ArazzoValue.FromLiteral(null) });
        }

        return obj.ContainsKey("reference")
            ? ArazzoReferenceable<ArazzoParameter>.Of(ReadReusableObject(obj))
            : ArazzoReferenceable<ArazzoParameter>.Of(ReadParameter(obj, path, ctx));
    }

    private static ArazzoReferenceable<ArazzoSuccessAction> ReadSuccessActionReferenceable(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Success action (or reference) must be an object.");
            return ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = string.Empty, Type = string.Empty });
        }

        return obj.ContainsKey("reference")
            ? ArazzoReferenceable<ArazzoSuccessAction>.Of(ReadReusableObject(obj))
            : ArazzoReferenceable<ArazzoSuccessAction>.Of(ReadSuccessAction(obj, path, ctx));
    }

    private static ArazzoReferenceable<ArazzoFailureAction> ReadFailureActionReferenceable(JsonNode? node, string path, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error(path, "Failure action (or reference) must be an object.");
            return ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoFailureAction { Name = string.Empty, Type = string.Empty });
        }

        return obj.ContainsKey("reference")
            ? ArazzoReferenceable<ArazzoFailureAction>.Of(ReadReusableObject(obj))
            : ArazzoReferenceable<ArazzoFailureAction>.Of(ReadFailureAction(obj, path, ctx));
    }

    private static ArazzoReusableObject ReadReusableObject(JsonObject obj) => new()
    {
        Reference = ReadOptionalString(obj, "reference") ?? string.Empty,
        Value = ReadOptionalString(obj, "value"),
    };

    private static ArazzoValue ReadValue(JsonNode? node)
    {
        if (node is JsonObject obj && obj.ContainsKey("context") && obj.ContainsKey("selector") && obj.ContainsKey("type"))
        {
            return ArazzoValue.FromSelector(ReadSelector(obj));
        }

        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            var s = value.GetValue<string>();
            if (s.StartsWith('$'))
            {
                return ArazzoValue.FromExpression(s);
            }
        }

        return ArazzoValue.FromLiteral(node?.DeepClone());
    }

    private static ArazzoSelector ReadSelector(JsonObject obj) => new()
    {
        Context = ReadOptionalString(obj, "context") ?? string.Empty,
        Selector = ReadOptionalString(obj, "selector") ?? string.Empty,
        Type = ReadSelectorType(Get(obj, "type")) ?? ArazzoSelectorType.Simple,
    };

    private static ArazzoSelectorType? ReadSelectorType(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonValue v when v.GetValueKind() == JsonValueKind.String:
                return new ArazzoSelectorType { Type = v.GetValue<string>() };
            case JsonObject obj:
                return new ArazzoSelectorType
                {
                    Type = ReadOptionalString(obj, "type") ?? string.Empty,
                    Version = ReadOptionalString(obj, "version"),
                };
            default:
                return null;
        }
    }

    private static Dictionary<string, ArazzoValue>? ReadValueMap(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return null;
        }

        var map = new Dictionary<string, ArazzoValue>(StringComparer.Ordinal);
        foreach (var (key, value) in obj)
        {
            map[key] = ReadValue(value);
        }

        return map;
    }

    private static List<string>? ReadStringArray(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in array)
        {
            if (item is JsonValue v && v.GetValueKind() == JsonValueKind.String)
            {
                list.Add(v.GetValue<string>());
            }
        }

        return list;
    }

    private static List<T>? ReadArray<T>(JsonNode? node, string path, ParsingContext ctx, Func<JsonNode?, string, ParsingContext, T> itemReader)
    {
        if (node is null)
        {
            return null;
        }

        if (node is not JsonArray array)
        {
            ctx.Error(path, "Expected an array.");
            return null;
        }

        var list = new List<T>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            list.Add(itemReader(array[i], $"{path}/{i}", ctx));
        }

        return list;
    }

    private static List<T> ReadRequiredArray<T>(JsonNode? node, string path, ParsingContext ctx, Func<JsonNode?, string, ParsingContext, T> itemReader)
    {
        var list = ReadArray(node, path, ctx, itemReader);
        if (list is not null)
        {
            return list;
        }

        ctx.Error(path, "Missing required array.");
        return new List<T>();
    }

    private static string ReadRequiredString(JsonObject obj, string name, string path, ParsingContext ctx)
    {
        if (Get(obj, name) is JsonValue v && v.GetValueKind() == JsonValueKind.String)
        {
            return v.GetValue<string>();
        }

        ctx.Error(path, $"Missing required string field '{name}'.");
        return string.Empty;
    }

    private static string? ReadOptionalString(JsonObject obj, string name) =>
        Get(obj, name) is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    private static int? ReadOptionalInt(JsonObject obj, string name, string path, ParsingContext ctx)
    {
        if (Get(obj, name) is not JsonValue v || v.GetValueKind() != JsonValueKind.Number)
        {
            return null;
        }

        try
        {
            return v.GetValue<int>();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException)
        {
            ctx.Error(path, $"Field '{name}' must be an integer.");
            return null;
        }
    }

    private static JsonObject? ReadOptionalObject(JsonObject obj, string name, string path, ParsingContext ctx)
    {
        var node = Get(obj, name);
        if (node is null)
        {
            return null;
        }

        if (node is JsonObject o)
        {
            return o;
        }

        ctx.Error(path, $"Field '{name}' must be an object.");
        return null;
    }

    private static double? ReadOptionalDouble(JsonObject obj, string name) =>
        Get(obj, name) is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? v.GetValue<double>() : null;

    private static Dictionary<string, JsonNode?>? ReadExtensions(JsonObject obj, string path, HashSet<string> known, ParsingContext ctx)
    {
        Dictionary<string, JsonNode?>? extensions = null;
        foreach (var (key, value) in obj)
        {
            if (known.Contains(key))
            {
                continue;
            }

            if (!ctx.Settings.IgnoreUnrecognizedFields)
            {
                ctx.Warn($"{path}/{key}", $"Unrecognized field '{key}'.");
            }

            extensions ??= new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            extensions[key] = value?.DeepClone();
        }

        return extensions;
    }

    private static JsonNode? Get(JsonObject obj, string name) => obj.TryGetPropertyValue(name, out var value) ? value : null;
}
