namespace Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

public class SubscribeOperationAttribute : OperationAttribute
{
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

    public SubscribeOperationAttribute()
    {
        OperationType = OperationType.Subscribe;
    }
}
