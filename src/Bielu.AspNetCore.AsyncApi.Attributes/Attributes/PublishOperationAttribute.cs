namespace Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

public class PublishOperationAttribute : OperationAttribute
{
    public PublishOperationAttribute(Type messagePayloadType, params string[] tags)
    {
        OperationType = OperationType.Publish;
        MessagePayloadType = messagePayloadType;
        Tags = tags;
    }
    public PublishOperationAttribute(Type messagePayloadType)
    {
        OperationType = OperationType.Publish;
        MessagePayloadType = messagePayloadType;
    }

    public PublishOperationAttribute()
    {
        OperationType = OperationType.Publish;
    }
}
