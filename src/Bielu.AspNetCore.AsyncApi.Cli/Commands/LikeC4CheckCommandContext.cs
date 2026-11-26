namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Contains the context for the likec4 check command, including all parsed command-line arguments.
/// </summary>
internal sealed class LikeC4CheckCommandContext
{
    /// <summary>The LikeC4 model, as the JSON <c>likec4 export json</c> writes: a path or URL.</summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// The AsyncAPI documents, each <c>[name=]&lt;path|url&gt;</c>, named the way the model's <c>asyncapi</c> metadata
    /// names them.
    /// </summary>
    public List<string> Sources { get; } = [];

    /// <summary>Whether warnings fail the check too.</summary>
    public bool Strict { get; set; }

    /// <summary>The output format (text or json).</summary>
    public string Format { get; set; } = "text";
}
