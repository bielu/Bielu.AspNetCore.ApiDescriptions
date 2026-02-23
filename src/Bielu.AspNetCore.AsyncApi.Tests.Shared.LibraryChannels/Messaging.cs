using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

namespace Bielu.AspNetCore.AsyncApi.Tests.LibraryChannels
{
    public interface IStreetlightMessageBus
    {
        /// <summary>
/// Publishes a light measurement event to the message bus.
/// </summary>
/// <param name="lightMeasuredEvent">The external event containing the light measurement details.</param>
void PublishLightMeasurement(ExternalEvent lightMeasuredEvent);
    }

    [AsyncApi]
    public class ExternaltMessageBus : IStreetlightMessageBus
    {
        private const string SubscribeLightMeasuredTopic = "subscribe/external/events";

      

        /// <summary>
        /// Publishes an external light measurement event to the configured message channel.
        /// </summary>
        /// <param name="lightMeasuredEvent">The external event containing the light measurement data to publish.</param>
        [Channel(SubscribeLightMeasuredTopic, Servers = new[] { "test-server" }, BindingsRef = "amqpDev")]
        [SubscribeOperation(typeof(ExternalEvent), "External", Summary = "Subscribe to external events.")]
        public void PublishLightMeasurement(ExternalEvent lightMeasuredEvent)
        {
            var payload = JsonSerializer.Serialize(lightMeasuredEvent);
    
        }
    }

    public class ExternalEvent
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}