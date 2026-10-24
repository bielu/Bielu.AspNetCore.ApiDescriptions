using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers;

namespace Bielu.AspNetCore.AsyncApi.Merger.Merge;

/// <summary>
/// Creates the <see cref="AsyncApiReaderSettings"/> used wherever a serialized AsyncAPI document is read back
/// (merging, and the CLI's validate and diff commands).
/// </summary>
/// <remarks>
/// A default <see cref="AsyncApiReaderSettings"/> registers no binding parsers, so the reader silently drops every
/// server, channel, operation and message binding. Registering ByteBard's standard bindings (Kafka, AMQP, MQTT,
/// HTTP, WebSockets, SNS, SQS, Pulsar) keeps them.
/// </remarks>
internal static class AsyncApiReaderSettingsFactory
{
    public static AsyncApiReaderSettings Create()
    {
        var settings = new AsyncApiReaderSettings();
        settings.Bindings.Add(BindingsCollection.All);
        return settings;
    }
}
