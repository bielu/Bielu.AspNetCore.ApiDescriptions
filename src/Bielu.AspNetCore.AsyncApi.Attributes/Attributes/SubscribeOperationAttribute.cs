namespace Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

public class SubscribeOperationAttribute : OperationAttribute
{
    /// <summary>
    /// Initializes a SubscribeOperationAttribute with the specified message payload type and optional tags.
    /// </summary>
    /// <param name="messagePayloadType">The CLR type of messages for the subscription.</param>
    /// <param name="tags">Optional tags to associate with the operation.</param>
    public SubscribeOperationAttribute(Type messagePayloadType, params string[] tags)
    {
        OperationType = OperationType.Subscribe;
        MessagePayloadType = messagePayloadType;
        Tags = tags;
    }

    public SubscribeOperationAttribute(Type messagePayloadType)
    {
        OperationType = OperationType.Subscribe;
        MessagePayloadType = messagePayloadType;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SubscribeOperationAttribute"/> and sets the operation type to Subscribe.
    /// </summary>
    public SubscribeOperationAttribute()
    {
        OperationType = OperationType.Subscribe;
    }
}