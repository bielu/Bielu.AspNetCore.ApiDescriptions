namespace Bielu.AspNetCore.AsyncApi.Attributes;

/// <summary>
/// Marks an operation through which the application <em>sends</em> messages to the channel
/// (AsyncAPI 3 <c>action: send</c>; written as a <c>subscribe</c> operation in an AsyncAPI 2.x document).
/// </summary>
/// <example>
/// <code>
/// [Channel("light/measured")]
/// [SendOperation(typeof(LightMeasuredEvent), Summary = "Publishes light measurements.")]
/// public void PublishLightMeasurement(LightMeasuredEvent payload) { }
/// </code>
/// </example>
public class SendOperationAttribute : OperationAttribute
{
    /// <summary>
    /// Initializes a <see cref="SendOperationAttribute"/> with the payload type of the messages sent and optional tags.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of the messages the application sends.</param>
    /// <param name="tags">Optional tags to associate with the operation.</param>
    public SendOperationAttribute(Type messagePayloadType, params string[] tags)
    {
        OperationType = OperationType.Send;
        MessagePayloadType = messagePayloadType;
        Tags = tags;
    }

    /// <summary>
    /// Initializes a <see cref="SendOperationAttribute"/> with the payload type of the messages sent.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of the messages the application sends.</param>
    public SendOperationAttribute(Type messagePayloadType)
    {
        OperationType = OperationType.Send;
        MessagePayloadType = messagePayloadType;
    }

    /// <summary>
    /// Initializes a <see cref="SendOperationAttribute"/> whose messages come from <see cref="MessageAttribute"/>s on the same member.
    /// </summary>
    public SendOperationAttribute()
    {
        OperationType = OperationType.Send;
    }
}
