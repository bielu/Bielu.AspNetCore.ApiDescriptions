namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Contains the context for the likec4 command, including all parsed command-line arguments.
/// </summary>
internal sealed class LikeC4CommandContext
{
    /// <summary>
    /// The AsyncAPI documents, each <c>[name=]&lt;path|url&gt;</c>. Without a name, a path may be a glob, and each
    /// document is named after its file; a named path is read as it is.
    /// </summary>
    public List<string> Sources { get; } = [];

    /// <summary>
    /// The links of the documents, each <c>name=&lt;url&gt;</c>, for documents whose readers find them
    /// somewhere other than where the command reads them.
    /// </summary>
    public List<string> Links { get; } = [];

    /// <summary>
    /// The Arazzo documents whose workflows to draw as dynamic views, each <c>[name=]&lt;path|url&gt;</c>, read the way
    /// <see cref="Sources"/> are.
    /// </summary>
    public List<string> Workflows { get; } = [];

    /// <summary>
    /// The output <c>.c4</c> file.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
}
