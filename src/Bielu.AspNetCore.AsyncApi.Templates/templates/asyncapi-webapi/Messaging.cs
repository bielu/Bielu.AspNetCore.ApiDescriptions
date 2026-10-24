using Bielu.AspNetCore.AsyncApi.Attributes;

namespace AsyncApiWebApi;

[AsyncApi]
public class NotificationService
{
    /// <summary>
    /// Publishes a notification update.
    /// </summary>
    /// <param name="update">The notification update data.</param>
    [Channel("notifications/updates")]
    [SendOperation(typeof(NotificationUpdate), "Notify", Summary = "Subscribe to notification updates.")]
    public void Notify(NotificationUpdate update) { }
}

/// <summary>
/// Represents a notification update.
/// </summary>
/// <param name="Message">The notification message.</param>
/// <param name="Timestamp">When the notification was generated.</param>
public record NotificationUpdate(string Message, DateTime Timestamp);
