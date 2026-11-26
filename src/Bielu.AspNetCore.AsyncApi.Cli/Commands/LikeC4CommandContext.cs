namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Contains the context for the likec4 command, including all parsed command-line arguments.
/// </summary>
internal sealed class LikeC4CommandContext
{
    /// <summary>
    /// The AsyncAPI documents, each <c>[name=]&lt;path|url&gt;</c>. A path may be a glob; without a name, a document
    /// is named after its file.
    /// </summary>
    public List<string> Sources { get; } = [];

    /// <summary>
    /// The links of the service elements, each <c>name=&lt;url&gt;</c>, for documents whose readers find them
    /// somewhere other than where the command reads them.
    /// </summary>
    public List<string> Links { get; } = [];

    /// <summary>
    /// The output <c>.c4</c> file.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
}
