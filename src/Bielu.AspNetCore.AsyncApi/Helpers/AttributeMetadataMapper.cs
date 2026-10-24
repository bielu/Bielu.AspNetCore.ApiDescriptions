using Bielu.AspNetCore.AsyncApi.Attributes;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Helpers;

/// <summary>
/// Maps the values declared on attributes onto AsyncAPI objects. Shared by the attribute pipeline and by
/// integrations (such as Wolverine) that build messages themselves but honor <see cref="MessageAttribute"/>.
/// </summary>
internal static class AttributeMetadataMapper
{
    /// <summary>
    /// Attaches a message bindings item registered in <c>components/messageBindings</c> (via
    /// <see cref="AsyncApiOptions.AddMessageBinding"/>) to <paramref name="message"/>.
    /// </summary>
    public static void AttachMessageBindings(AsyncApiDocument document, AsyncApiMessage message, string? bindingsRef)
    {
        if (string.IsNullOrWhiteSpace(bindingsRef))
        {
            return;
        }

        if (document.Components?.MessageBindings is { } registered &&
            registered.TryGetValue(AsyncApiNamingHelper.SanitizeKey(bindingsRef), out var bindings))
        {
            message.Bindings = bindings;
        }
    }

    public static AsyncApiExternalDocumentation? CreateExternalDocs(string? url, string? description) =>
        string.IsNullOrWhiteSpace(url)
            ? null
            : new AsyncApiExternalDocumentation { Url = new Uri(url, UriKind.RelativeOrAbsolute), Description = description };

    /// <summary>
    /// Adds each tag name to <paramref name="target"/> and registers it in <c>components/tags</c> when it is
    /// not there yet.
    /// </summary>
    public static void AddTags(AsyncApiDocument document, IList<AsyncApiTag> target, string[] tagNames)
    {
        foreach (var tagName in tagNames)
        {
            target.Add(new AsyncApiTag { Name = tagName });

            document.Components.Tags ??= new Dictionary<string, AsyncApiTag>();
            if (!document.Components.Tags.ContainsKey(tagName))
            {
                document.Components.Tags[tagName] = new AsyncApiTag { Name = tagName };
            }
        }
    }

    /// <summary>
    /// Applies everything <paramref name="messageAttr"/> declares that does not need a schema:
    /// content type, bindings, external docs, traits, correlation id and tags. The headers type is left to
    /// the caller, since resolving it needs the schema service.
    /// </summary>
    public static void ApplyMessageAttribute(AsyncApiDocument document, AsyncApiMessage message, MessageAttribute messageAttr)
    {
        if (messageAttr.ContentType is not null)
        {
            message.ContentType = messageAttr.ContentType;
        }

        AttachMessageBindings(document, message, messageAttr.BindingsRef);

        message.ExternalDocs = CreateExternalDocs(messageAttr.ExternalDocsUrl, messageAttr.ExternalDocsDescription);

        foreach (var traitKey in messageAttr.Traits)
        {
            message.Traits ??= new List<AsyncApiMessageTrait>();
            message.Traits.Add(new AsyncApiMessageTraitReference(
                $"#/components/messageTraits/{AsyncApiNamingHelper.SanitizeKey(traitKey)}"));
        }

        if (messageAttr.CorrelationIdLocation is { Length: > 0 } correlationIdLocation)
        {
            message.CorrelationId = new AsyncApiCorrelationId
            {
                Location = correlationIdLocation,
                Description = messageAttr.CorrelationIdDescription
            };
        }

        if (messageAttr.Tags is { Length: > 0 })
        {
            message.Tags ??= new List<AsyncApiTag>();
            AddTags(document, message.Tags, messageAttr.Tags);
        }
    }
}
