namespace Bielu.AspNetCore.AsyncApi.Attributes;

/// <summary>
/// An AsyncAPI 2.x <c>subscribe</c> operation: one that other applications subscribe to, so this application
/// <em>sends</em> the messages. Superseded by <see cref="SendOperationAttribute"/>, which produces
/// the same operation and is named from the application's own point of view, as in AsyncAPI 3.
/// </summary>
[Obsolete("Use [SendOperation]. A 2.x subscribe operation is one where the application sends, so " +
          "[SubscribeOperation] maps to [SendOperation], not [ReceiveOperation].")]
public class SubscribeOperationAttribute : OperationAttribute
{
    /// <summary>
    /// Initializes a <see cref="SubscribeOperationAttribute"/> with the payload type of the messages sent and optional tags.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of the messages the application sends.</param>
    /// <param name="tags">Optional tags to associate with the operation.</param>
    public SubscribeOperationAttribute(Type messagePayloadType, params string[] tags)
    {
        OperationType = OperationType.Send;
        MessagePayloadType = messagePayloadType;
        Tags = tags;
    }

    /// <summary>
    /// Initializes a <see cref="SubscribeOperationAttribute"/> with the payload type of the messages sent.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of the messages the application sends.</param>
    public SubscribeOperationAttribute(Type messagePayloadType)
    {
        OperationType = OperationType.Send;
        MessagePayloadType = messagePayloadType;
    }

    /// <summary>
    /// Initializes a <see cref="SubscribeOperationAttribute"/> whose messages come from <see cref="MessageAttribute"/>s on the same member.
    /// </summary>
    public SubscribeOperationAttribute()
    {
        OperationType = OperationType.Send;
    }
}
