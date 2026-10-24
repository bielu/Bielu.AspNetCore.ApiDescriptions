using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Attributes;

namespace Bielu.AspNetCore.AsyncApi.Models.Metadata;

public record AsyncApiTypeMetadata(
    Type Type,
    AsyncApiAttribute AsyncApi,
    IReadOnlyList<AsyncApiMemberMetadata> Members
);

public record AsyncApiMemberMetadata(
    MemberInfo Member,
    ChannelAttribute? Channel,
    IReadOnlyList<ChannelParameterAttribute> Parameters,
    IReadOnlyList<MessageAttribute> Messages,
    IReadOnlyList<OperationAttribute> Operations,
    IReadOnlyList<MessageExampleAttribute> MessageExamples
);
