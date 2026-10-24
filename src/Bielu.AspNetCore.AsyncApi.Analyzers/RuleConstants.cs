namespace Bielu.AspNetCore.AsyncApi.Analyzers;

internal static class RuleConstants
{
    public const string Namespace = "Bielu.AspNetCore.AsyncApi.Attributes";
    public const string AsyncApiAttributeName = Namespace + ".AsyncApiAttribute";
    public const string ChannelAttributeName = Namespace + ".ChannelAttribute";
    public const string OperationAttributeName = Namespace + ".OperationAttribute";
    public const string MessageAttributeName = Namespace + ".MessageAttribute";
    public const string ChannelParameterAttributeName = Namespace + ".ChannelParameterAttribute";
    public const string MessageExampleAttributeName = Namespace + ".MessageExampleAttribute";
    public const string IMessageExampleProviderName = Namespace + ".IAsyncApiMessageExampleProvider";
}
