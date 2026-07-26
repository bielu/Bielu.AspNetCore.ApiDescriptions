namespace Bielu.Arazzo.Expressions;

/// <summary>Parses spec §5.9 Runtime Expression strings into a <see cref="RuntimeExpression"/> AST.</summary>
public static class RuntimeExpressionParser
{
    public static bool TryParse(string input, out RuntimeExpression? expression, out string? error)
    {
        expression = null;
        error = null;

        if (string.IsNullOrEmpty(input) || input[0] != '$')
        {
            error = "Runtime expressions must start with '$'.";
            return false;
        }

        switch (input)
        {
            case "$url": expression = new RuntimeExpression.Url(input); return true;
            case "$method": expression = new RuntimeExpression.Method(input); return true;
            case "$statusCode": expression = new RuntimeExpression.StatusCode(input); return true;
            case "$self": expression = new RuntimeExpression.Self(input); return true;
        }

        if (TryConsumePrefix(input, "$request.", out var afterRequest))
        {
            return TryParseSource(input, afterRequest, s => new RuntimeExpression.Request(input, s), out expression, out error);
        }

        if (TryConsumePrefix(input, "$response.", out var afterResponse))
        {
            return TryParseSource(input, afterResponse, s => new RuntimeExpression.Response(input, s), out expression, out error);
        }

        if (TryConsumePrefix(input, "$message.", out var afterMessage))
        {
            return TryParseSource(input, afterMessage, s => new RuntimeExpression.Message(input, s), out expression, out error);
        }

        if (TryConsumePrefix(input, "$inputs.", out var afterInputs))
        {
            return TryParseNameWithPointer(input, afterInputs, (name, ptr) => new RuntimeExpression.Inputs(input, name, ptr), out expression, out error);
        }

        if (TryConsumePrefix(input, "$outputs.", out var afterOutputs))
        {
            return TryParseNameWithPointer(input, afterOutputs, (name, ptr) => new RuntimeExpression.Outputs(input, name, ptr), out expression, out error);
        }

        if (TryConsumePrefix(input, "$steps.", out var afterSteps))
        {
            return TryParseSteps(input, afterSteps, out expression, out error);
        }

        if (TryConsumePrefix(input, "$workflows.", out var afterWorkflows))
        {
            return TryParseWorkflows(input, afterWorkflows, out expression, out error);
        }

        if (TryConsumePrefix(input, "$sourceDescriptions.", out var afterSource))
        {
            return TryParseSourceDescriptions(input, afterSource, out expression, out error);
        }

        if (TryConsumePrefix(input, "$components.", out var afterComponents))
        {
            return TryParseComponents(input, afterComponents, out expression, out error);
        }

        error = $"Unrecognized runtime expression: '{input}'.";
        return false;
    }

    private static bool TryParseSource(
        string raw,
        string rest,
        Func<RuntimeExpressionSource, RuntimeExpression> factory,
        out RuntimeExpression? expression,
        out string? error)
    {
        expression = null;
        error = null;

        if (TryConsumePrefix(rest, "header.", out var headerName) && headerName.Length > 0)
        {
            expression = factory(new RuntimeExpressionSource(RuntimeExpressionSourceKind.Header, headerName, null));
            return true;
        }

        if (TryConsumePrefix(rest, "query.", out var queryName) && queryName.Length > 0)
        {
            expression = factory(new RuntimeExpressionSource(RuntimeExpressionSourceKind.Query, queryName, null));
            return true;
        }

        if (TryConsumePrefix(rest, "path.", out var pathName) && pathName.Length > 0)
        {
            expression = factory(new RuntimeExpressionSource(RuntimeExpressionSourceKind.Path, pathName, null));
            return true;
        }

        if (rest == "body" || rest.StartsWith("body#", StringComparison.Ordinal))
        {
            var (_, pointer) = SplitPointer(rest);
            expression = factory(new RuntimeExpressionSource(RuntimeExpressionSourceKind.Body, null, pointer));
            return true;
        }

        if (rest == "payload" || rest.StartsWith("payload#", StringComparison.Ordinal))
        {
            var (_, pointer) = SplitPointer(rest);
            expression = factory(new RuntimeExpressionSource(RuntimeExpressionSourceKind.Payload, null, pointer));
            return true;
        }

        error = $"'{raw}' has an unrecognized source; expected header./query./path./body/payload.";
        return false;
    }

    private static bool TryParseNameWithPointer(
        string raw,
        string rest,
        Func<string, string?, RuntimeExpression> factory,
        out RuntimeExpression? expression,
        out string? error)
    {
        expression = null;
        error = null;

        var (name, pointer) = SplitPointer(rest);
        if (name.Length == 0)
        {
            error = $"'{raw}' is missing a name.";
            return false;
        }

        expression = factory(name, pointer);
        return true;
    }

    private static bool TryParseSteps(string raw, string rest, out RuntimeExpression? expression, out string? error)
    {
        expression = null;
        error = null;

        var (withoutPointer, pointer) = SplitPointer(rest);
        var parts = withoutPointer.Split('.');
        if (parts.Length != 3 || parts[1] != "outputs" || parts[0].Length == 0 || parts[2].Length == 0)
        {
            error = $"'{raw}' must be in the form '$steps.<stepId>.outputs.<outputName>'.";
            return false;
        }

        expression = new RuntimeExpression.Steps(raw, parts[0], parts[2], pointer);
        return true;
    }

    private static bool TryParseWorkflows(string raw, string rest, out RuntimeExpression? expression, out string? error)
    {
        expression = null;
        error = null;

        var (withoutPointer, pointer) = SplitPointer(rest);
        var parts = withoutPointer.Split('.', 3);
        if (parts.Length < 2 || (parts[1] != "inputs" && parts[1] != "outputs") || parts[0].Length == 0)
        {
            error = $"'{raw}' must be in the form '$workflows.<workflowName>.(inputs|outputs)[.<fieldName>]'.";
            return false;
        }

        var fieldName = parts.Length > 2 ? parts[2] : null;
        expression = new RuntimeExpression.Workflows(raw, parts[0], parts[1], fieldName, pointer);
        return true;
    }

    private static bool TryParseSourceDescriptions(string raw, string rest, out RuntimeExpression? expression, out string? error)
    {
        expression = null;
        error = null;

        var dot = rest.IndexOf('.');
        if (dot < 0)
        {
            error = $"'{raw}' is missing a source name and reference id.";
            return false;
        }

        var sourceName = rest[..dot];
        var referenceId = rest[(dot + 1)..];
        if (sourceName.Length == 0 || referenceId.Length == 0)
        {
            error = $"'{raw}' is missing a source name or reference id.";
            return false;
        }

        expression = new RuntimeExpression.SourceDescriptions(raw, sourceName, referenceId);
        return true;
    }

    private static bool TryParseComponents(string raw, string rest, out RuntimeExpression? expression, out string? error)
    {
        expression = null;
        error = null;

        var dot = rest.IndexOf('.');
        if (dot < 0)
        {
            error = $"'{raw}' is missing a components field and name.";
            return false;
        }

        var field = rest[..dot];
        var name = rest[(dot + 1)..];
        if (field.Length == 0 || name.Length == 0)
        {
            error = $"'{raw}' is missing a components field or name.";
            return false;
        }

        expression = new RuntimeExpression.Components(raw, field, name);
        return true;
    }

    private static bool TryConsumePrefix(string input, string prefix, out string rest)
    {
        if (input.StartsWith(prefix, StringComparison.Ordinal))
        {
            rest = input[prefix.Length..];
            return true;
        }

        rest = string.Empty;
        return false;
    }

    private static (string Value, string? Pointer) SplitPointer(string input)
    {
        var hashIndex = input.IndexOf('#');
        return hashIndex < 0 ? (input, null) : (input[..hashIndex], input[(hashIndex + 1)..]);
    }
}
