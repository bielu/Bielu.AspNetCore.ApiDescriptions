namespace Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface)]
public abstract class OperationAttribute : Attribute
{
    public OperationType OperationType { get; protected set; }

    public Type? MessagePayloadType { get; protected set; }

    /// <summary>
    /// A short summary of what the operation is about.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Unique string used to identify the operation.
    /// The id MUST be unique among all operations described in the API.
    /// The operationId value is case-sensitive.
    /// Tools and libraries MAY use the operationId to uniquely identify an operation,
    /// therefore, it is RECOMMENDED to follow common programming naming conventions.
    /// </summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// A verbose explanation of the operation.
    /// CommonMark syntax can be used for rich text representation.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// A short title for the operation.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The name of an operation bindings item to reference.
    /// The bindings must be added to components/operationBindings with the same name.
    /// </summary>
    public string? BindingsRef { get; set; }

    /// <summary>
    /// URL of additional external documentation. Setting it adds an <c>externalDocs</c> object.
    /// </summary>
    public string? ExternalDocsUrl { get; set; }

    /// <summary>
    /// A description of the external documentation. Only used when <see cref="ExternalDocsUrl"/> is set.
    /// </summary>
    public string? ExternalDocsDescription { get; set; }

    /// <summary>
    /// The keys of the security schemes this operation requires. Each must be registered in
    /// <c>components/securitySchemes</c>, for example with <c>AsyncApiOptions.AddSecurityScheme</c>.
    /// </summary>
    public string[] SecuritySchemes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The keys of the operation traits to apply to this operation. Each must be registered in
    /// <c>components/operationTraits</c>, for example with <c>AsyncApiOptions.AddOperationTrait</c>.
    /// </summary>
    public string[] Traits { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The channel the reply to this operation is sent on, as declared with <see cref="ChannelAttribute"/>.
    /// Setting it (or a reply address) adds a <c>reply</c> object to the operation. AsyncAPI 3.x only.
    /// </summary>
    public string? ReplyChannel { get; set; }

    /// <summary>
    /// A runtime expression that specifies where the reply is sent, for example <c>$message.header#/replyTo</c>.
    /// </summary>
    public string? ReplyAddressLocation { get; set; }

    /// <summary>
    /// An optional description of the reply address. Only used when <see cref="ReplyAddressLocation"/> is set.
    /// </summary>
    public string? ReplyAddressDescription { get; set; }

    /// <summary>
    /// The message ids of the reply, which must be messages of <see cref="ReplyChannel"/>.
    /// </summary>
    public string[] ReplyMessageIds { get; set; } = Array.Empty<string>();

    /// <summary>
    /// A list of tags for API documentation control. Tags can be used for logical grouping of operations.
    /// </summary>
    public string[] Tags { get; protected set; } = Array.Empty<string>();
}
