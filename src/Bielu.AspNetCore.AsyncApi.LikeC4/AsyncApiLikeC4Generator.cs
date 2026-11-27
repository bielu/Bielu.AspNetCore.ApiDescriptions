using Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

namespace Bielu.AspNetCore.AsyncApi.LikeC4;

/// <summary>
/// Generates a <see href="https://likec4.dev">LikeC4</see> model from AsyncAPI documents: an element per service and
/// per channel, a relationship per operation, the service-to-service flows the channels imply, and views of them.
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
/// </remarks>
/// <example>
/// <code>
/// var model = AsyncApiLikeC4Generator.Generate(
/// [
///     new AsyncApiLikeC4Source("orders", File.ReadAllText("orders.json")),
///     new AsyncApiLikeC4Source("inventory", File.ReadAllText("inventory.json")),
/// ]);
/// File.WriteAllText("architecture/asyncapi.c4", model);
/// </code>
/// </example>
public static class AsyncApiLikeC4Generator
{
    private const string Header =
        """
        Generated from AsyncAPI documents by Bielu.AspNetCore.AsyncApi.LikeC4. Do not edit: changes are lost when it
        is generated again. Describe these elements further in a file of your own, with `extend <id> { ... }`.
        """;

    /// <summary>Generates the LikeC4 model of <paramref name="sources"/>, as LikeC4 DSL.</summary>
    /// <param name="sources">The documents, one per service, in the order their elements are written.</param>
    /// <returns>The contents of a <c>.c4</c> file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no sources, or two have the same name.</exception>
    /// <exception cref="FormatException">A source is not an AsyncAPI document.</exception>
    public static string Generate(IEnumerable<AsyncApiLikeC4Source> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var list = sources.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one AsyncAPI document is required.", nameof(sources));
        }

        if (list.GroupBy(source => source.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new ArgumentException($"Two AsyncAPI documents are named '{duplicate.Key}'.", nameof(sources));
        }

        var documents = list.Select(source => (source, Read(source))).ToList();
        return LikeC4Writer.Write(AsyncApiLikeC4ModelBuilder.Build(documents), Header);
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
}
