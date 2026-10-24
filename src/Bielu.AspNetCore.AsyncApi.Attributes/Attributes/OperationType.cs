namespace Bielu.AspNetCore.AsyncApi.Attributes;

/// <summary>
/// The action of an operation, from the application's point of view, as in AsyncAPI 3.
/// </summary>
public enum OperationType
{
    /// <summary>
    /// The application sends messages to the channel (AsyncAPI 3 <c>send</c>; a 2.x <c>subscribe</c> operation).
    /// </summary>
    Send,

    /// <summary>
    /// The application receives messages from the channel (AsyncAPI 3 <c>receive</c>; a 2.x <c>publish</c> operation).
    /// </summary>
    Receive
}
