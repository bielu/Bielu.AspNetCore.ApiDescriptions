using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

namespace Bielu.AspNetCore.AsyncApi.Models.Metadata;

public record AsyncApiTypeMetadata(
    Type Type,
    AsyncApiAttribute AsyncApi,
    List<AsyncApiMemberMetadata> Members
);

public record AsyncApiMemberMetadata(
    MemberInfo Member,
    ChannelAttribute? Channel,
    List<ChannelParameterAttribute> Parameters,
    List<MessageAttribute> Messages,
    List<OperationAttribute> Operations,
    List<MessageExampleAttribute> MessageExamples
);
