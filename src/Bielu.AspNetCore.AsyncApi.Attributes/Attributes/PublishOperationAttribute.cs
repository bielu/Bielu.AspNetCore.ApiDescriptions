namespace Bielu.AspNetCore.AsyncApi.Attributes;

/// <summary>
/// An AsyncAPI 2.x <c>publish</c> operation: one that other applications publish to, so this application
/// <em>receives</em> the messages. Superseded by <see cref="ReceiveOperationAttribute"/>, which produces
/// the same operation and is named from the application's own point of view, as in AsyncAPI 3.
/// </summary>
[Obsolete("Use [ReceiveOperation]. A 2.x publish operation is one where the application receives, so " +
          "[PublishOperation] maps to [ReceiveOperation], not [SendOperation].")]
public class PublishOperationAttribute : OperationAttribute
{
    /// <summary>
    /// Initializes a <see cref="PublishOperationAttribute"/> with the payload type of the messages received and optional tags.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of the messages the application receives.</param>
    /// <param name="tags">Optional tags to associate with the operation.</param>
    public PublishOperationAttribute(Type messagePayloadType, params string[] tags)
    {
        OperationType = OperationType.Receive;
        MessagePayloadType = messagePayloadType;
        Tags = tags;
    }

    /// <summary>
    /// Initializes a <see cref="PublishOperationAttribute"/> with the payload type of the messages received.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of the messages the application receives.</param>
    public PublishOperationAttribute(Type messagePayloadType)
    {
        OperationType = OperationType.Receive;
        MessagePayloadType = messagePayloadType;
    }

    /// <summary>
    /// Initializes a <see cref="PublishOperationAttribute"/> whose messages come from <see cref="MessageAttribute"/>s on the same member.
    /// </summary>
    public PublishOperationAttribute()
    {
        OperationType = OperationType.Receive;
    }
}
