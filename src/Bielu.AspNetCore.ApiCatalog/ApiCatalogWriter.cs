using System.Buffers;
using System.Text.Json;

namespace Bielu.AspNetCore.ApiCatalog;

/// <summary>Writes an API catalog as an RFC 9264 Linkset in its JSON form.</summary>
internal static class ApiCatalogWriter
{
    /// <summary>
    /// Writes the catalog: one link context for the catalog itself, whose <c>item</c> links are the APIs, and,
    /// when the app describes or documents its own API, one for that API with its <c>service-desc</c> and
    /// <c>service-doc</c> links.
    /// </summary>
    /// <param name="catalogUrl">The absolute URL the catalog is served from.</param>
    /// <param name="baseUrl">The app's absolute base URL, without a trailing slash.</param>
    /// <param name="serviceDescriptions">The documents describing the app's API.</param>
    /// <param name="options">The configured items and documentation pages.</param>
    public static byte[] Write(string catalogUrl, string baseUrl, IReadOnlyList<ApiCatalogLink> serviceDescriptions,
        ApiCatalogOptions options)
    {
        var apiUrl = Resolve(options.ApiUrl, baseUrl);
        var describesApi = serviceDescriptions.Count > 0 || options.ServiceDocs.Count > 0;
        List<ApiCatalogLink> items = describesApi
            ? [new ApiCatalogLink(apiUrl, Type: null, Title: null), .. options.Items]
            : options.Items;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("linkset");

            writer.WriteStartObject();
            writer.WriteString("anchor", catalogUrl);
            WriteLinks(writer, "item", items, baseUrl);
            writer.WriteEndObject();

            if (describesApi)
            {
                writer.WriteStartObject();
                writer.WriteString("anchor", apiUrl);
                WriteLinks(writer, "service-desc", serviceDescriptions, baseUrl);
                WriteLinks(writer, "service-doc", options.ServiceDocs, baseUrl);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteLinks(Utf8JsonWriter writer, string relation, IReadOnlyCollection<ApiCatalogLink> links,
        string baseUrl)
    {
        if (links.Count == 0)
        {
            return;
        }

        writer.WriteStartArray(relation);
        foreach (var link in links)
        {
            writer.WriteStartObject();
            writer.WriteString("href", Resolve(link.Href, baseUrl));
            if (link.Type is not null)
            {
                writer.WriteString("type", link.Type);
            }

            if (link.Title is not null)
            {
                writer.WriteString("title", link.Title);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>Makes <paramref name="href"/> absolute, resolving a path against the app's base URL.</summary>
    internal static string Resolve(string href, string baseUrl) =>
        Uri.TryCreate(href, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
            ? href
            : baseUrl + (href.StartsWith('/') ? href : "/" + href);
}
