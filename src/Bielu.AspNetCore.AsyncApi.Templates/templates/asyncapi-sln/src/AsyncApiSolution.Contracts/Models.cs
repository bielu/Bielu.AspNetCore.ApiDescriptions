using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

namespace AsyncApiSolution.Contracts;

/// <summary>
/// Represents a generic message in the system.
/// </summary>
/// <param name="Content">The message content.</param>
/// <param name="SentAt">When the message was sent.</param>
public record SystemMessage(string Content, DateTime SentAt);

[AsyncApi]
public interface IMessageContract
{
    /// <summary>
    /// Channel for system notifications.
    /// </summary>
    [Channel("system/notifications")]
    [SubscribeOperation(typeof(SystemMessage), "OnNotification", Summary = "Receive system notifications.")]
    void OnNotification(SystemMessage message);

    /// <summary>
    /// Channel for system commands.
    /// </summary>
    [Channel("system/commands")]
    [PublishOperation(typeof(SystemMessage), "SendCommand", Summary = "Send a system command.")]
    void SendCommand(SystemMessage message);
}
