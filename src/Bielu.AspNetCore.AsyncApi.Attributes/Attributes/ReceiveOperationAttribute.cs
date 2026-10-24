namespace Bielu.AspNetCore.AsyncApi.Attributes;

/// <summary>
/// Marks an operation through which the application <em>receives</em> messages from the channel
/// (AsyncAPI 3 <c>action: receive</c>; written as a <c>publish</c> operation in an AsyncAPI 2.x document).
/// </summary>
/// <example>
/// <code>
/// [Channel("light/measured")]
/// [ReceiveOperation(typeof(LightMeasuredEvent), Summary = "Receives light measurements.")]
/// public void OnLightMeasured(LightMeasuredEvent payload) { }
/// </code>
/// </example>
public class ReceiveOperationAttribute : OperationAttribute
{
    /// <summary>
    /// Initializes a <see cref="ReceiveOperationAttribute"/> with the payload type of the messages received and optional tags.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of the messages the application receives.</param>
    /// <param name="tags">Optional tags to associate with the operation.</param>
    public ReceiveOperationAttribute(Type messagePayloadType, params string[] tags)
    {
        OperationType = OperationType.Receive;
        MessagePayloadType = messagePayloadType;
        Tags = tags;
    }

    /// <summary>
    /// Initializes a <see cref="ReceiveOperationAttribute"/> with the payload type of the messages received.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of the messages the application receives.</param>
    public ReceiveOperationAttribute(Type messagePayloadType)
    {
        OperationType = OperationType.Receive;
        MessagePayloadType = messagePayloadType;
    }

    /// <summary>
    /// Initializes a <see cref="ReceiveOperationAttribute"/> whose messages come from <see cref="MessageAttribute"/>s on the same member.
    /// </summary>
    public ReceiveOperationAttribute()
    {
        OperationType = OperationType.Receive;
    }
}
