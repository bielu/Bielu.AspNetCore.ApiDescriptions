using Bielu.Arazzo.Models;
using Bielu.Arazzo.Readers;
using Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

namespace Bielu.AspNetCore.AsyncApi.LikeC4;

/// <summary>
/// Generates a <see href="https://likec4.dev">LikeC4</see> model from AsyncAPI documents: an element per service and
/// per channel, a relationship per operation, the service-to-service flows the channels imply, and views of them. Arazzo
/// workflows over those services become dynamic views.
/// </summary>
/// <remarks>
/// <para>
/// Each document is a service (element kind <c>asyncapiService</c>). Each channel is an element too
/// (<c>asyncapiChannel</c>), one per address across all documents, so the service that sends to <c>orders.created</c>
/// and the one that receives from it share it. An operation the service sends is a relationship from the service to
/// the channel (<c>asyncapiSend</c>); one it receives, from the channel to the service (<c>asyncapiReceive</c>). Every
/// sender of a channel is joined to every other service receiving the same messages from it (<c>asyncapiFlow</c>).
/// </para>
/// <para>
/// The model has a view of the services and channels (<c>asyncapiChannels</c>), one of the services and their flows
/// (<c>asyncapiServices</c>), and one per service. It is meant to be regenerated: describe its elements further in a
/// file of your own, with <c>extend &lt;id&gt; { … }</c>, rather than editing it.
/// </para>
/// <para>
/// Each Arazzo workflow is a dynamic view (<c>arazzo&lt;WorkflowId&gt;</c>), drawn as a sequence: every step is an
/// arrow from whoever makes it (the client, or the <c>x-actor</c> the step or workflow names) to what it calls. A
/// source description that is one of the AsyncAPI documents, by name or by its URL being the document's
/// <see cref="AsyncApiLikeC4Source.Link"/>, is that service, and a step on one of its operations sends to, or is
/// delivered from, the operation's channel. Other source descriptions become <c>arazzoSource</c> elements, and a step
/// invoking a workflow opens that workflow's view.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var model = AsyncApiLikeC4Generator.Generate(
/// [
///     new AsyncApiLikeC4Source("orders", File.ReadAllText("orders.json")),
///     new AsyncApiLikeC4Source("inventory", File.ReadAllText("inventory.json")),
/// ],
/// [
///     new ArazzoLikeC4Source("checkout", File.ReadAllText("checkout.arazzo.yaml")),
/// ]);
/// File.WriteAllText("architecture/asyncapi.c4", model);
/// </code>
/// </example>
public static class AsyncApiLikeC4Generator
{
    private const string Header =
        """
        Generated from KINDS documents by Bielu.AspNetCore.AsyncApi.LikeC4. Do not edit: changes are lost when it
        is generated again. Describe these elements further in a file of your own, with `extend <id> { ... }`.
        """;

    /// <summary>Generates the LikeC4 model of <paramref name="sources"/>, as LikeC4 DSL.</summary>
    /// <param name="sources">The documents, one per service, in the order their elements are written.</param>
    /// <returns>The contents of a <c>.c4</c> file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no sources, or two have the same name.</exception>
    /// <exception cref="FormatException">A source is not an AsyncAPI document.</exception>
    public static string Generate(IEnumerable<AsyncApiLikeC4Source> sources) => Generate(sources, []);

    /// <summary>
    /// Generates the LikeC4 model of <paramref name="sources"/>, with a dynamic view for every workflow of
    /// <paramref name="workflows"/>, as LikeC4 DSL.
    /// </summary>
    /// <param name="sources">The AsyncAPI documents, one per service, in the order their elements are written.</param>
    /// <param name="workflows">The Arazzo documents whose workflows to draw, in the order their views are written.</param>
    /// <returns>The contents of a <c>.c4</c> file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or <paramref name="workflows"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no documents at all, or two of a kind have the same name.</exception>
    /// <exception cref="FormatException">A source is not an AsyncAPI document, or a workflow source not an Arazzo one.</exception>
    public static string Generate(IEnumerable<AsyncApiLikeC4Source> sources, IEnumerable<ArazzoLikeC4Source> workflows)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(workflows);
        var asyncApi = sources.ToList();
        var arazzo = workflows.ToList();
        if (asyncApi.Count == 0 && arazzo.Count == 0)
        {
            throw new ArgumentException("At least one AsyncAPI or Arazzo document is required.", nameof(sources));
        }

        ThrowForDuplicateName(asyncApi.Select(source => source.Name), "AsyncAPI", nameof(sources));
        ThrowForDuplicateName(arazzo.Select(source => source.Name), "Arazzo", nameof(workflows));

        var build = new LikeC4Build();
        var index = AsyncApiLikeC4ModelBuilder.Build(build, asyncApi.Select(source => (source, Read(source))).ToList());
        ArazzoLikeC4ViewBuilder.Build(build, index, arazzo.Select(source => (source, Read(source))).ToList());
        var kinds = (asyncApi.Count > 0, arazzo.Count > 0) switch
        {
            (true, true) => "AsyncAPI and Arazzo",
            (true, false) => "AsyncAPI",
            _ => "Arazzo",
        };
        return LikeC4Writer.Write(build.Model, Header.Replace("KINDS", kinds, StringComparison.Ordinal));
    }

    private static void ThrowForDuplicateName(IEnumerable<string> names, string kind, string parameter)
    {
        if (names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new ArgumentException($"Two {kind} documents are named '{duplicate.Key}'.", parameter);
        }
    }

    private static AsyncApiServiceDescription Read(AsyncApiLikeC4Source source)
    {
        try
        {
            return AsyncApiDocumentReader.Read(source.Document);
        }
        catch (FormatException ex)
        {
            throw new FormatException($"AsyncAPI document '{source.Name}': {ex.Message}", ex);
        }
    }

    private static ArazzoDocument Read(ArazzoLikeC4Source source)
    {
        var result = ArazzoStringReader.Read(source.Document);
        if (result.Document is null || result.Diagnostics.Errors.Count > 0)
        {
            var reason = result.Diagnostics.Errors.FirstOrDefault()?.ToString() ?? "it could not be read";
            throw new FormatException($"Arazzo document '{source.Name}': {reason}");
        }

        return result.Document;
    }
}
