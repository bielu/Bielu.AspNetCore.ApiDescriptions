using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.CloudEvents;

/// <summary>Describes an AsyncAPI document's messages as CloudEvents.</summary>
public static class AsyncApiCloudEventsOptionsExtensions
{
    /// <summary>
    /// Describes every message of the document (or those <see cref="CloudEventsOptions.MessageFilter"/> keeps) as a
    /// CloudEvents 1.0 event. In <see cref="CloudEventsMode.Binary"/> mode, the default, the message's headers gain
    /// the required <c>id</c>, <c>source</c>, <c>specversion</c> and <c>type</c> attributes and the optional
    /// <c>dataschema</c>, <c>subject</c> and <c>time</c>, named as the message's protocol binding names them. In
    /// <see cref="CloudEventsMode.Structured"/> mode the payload becomes the <c>application/cloudevents+json</c>
    /// envelope, with the original payload as its <c>data</c>.
    /// </summary>
    /// <remarks>
    /// The event <c>type</c> is pinned with <c>const</c> to <see cref="CloudEventsOptions.TypePrefix"/> plus the message
    /// name, and <c>specversion</c> to <c>1.0</c>. Message examples gain the attributes too. The transformer runs where it
    /// is added among the document transformers, so add it after anything that adds messages.
    /// </remarks>
    /// <param name="options">The options of the document to describe.</param>
    /// <param name="configure">Configures the <see cref="CloudEventsOptions"/>.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddAsyncApi(options => options
    ///     .AddServer("kafka", "broker:9092", "kafka")
    ///     .AddCloudEvents(cloudEvents => cloudEvents
    ///         .WithSource("/orders")
    ///         .WithTypePrefix("com.example.orders.")));
    /// </code>
    /// </example>
    public static AsyncApiOptions AddCloudEvents(this AsyncApiOptions options, Action<CloudEventsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var cloudEventsOptions = new CloudEventsOptions();
        configure?.Invoke(cloudEventsOptions);
        return options.AddDocumentTransformer(new CloudEventsDocumentTransformer(cloudEventsOptions));
    }
}
