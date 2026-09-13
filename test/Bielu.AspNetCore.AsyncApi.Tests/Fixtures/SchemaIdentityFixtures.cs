using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

// Fixtures for the schema/message/operation identity collision tests in
// Integration/SchemaMessageIdentityCollisionTests.cs. Every [AsyncApi] type below declares an explicit,
// unique document name so it is only discovered when that specific test document is generated (an
// [AsyncApi] attribute with no name applies to *every* document, which would otherwise pollute the
// shared "asyncapi" document used by other tests).

namespace Bielu.AspNetCore.AsyncApi.Tests.Fixtures.SchemaIdentity.First
{
    /// <summary>Payload with the same short type name as <see cref="Bielu.AspNetCore.AsyncApi.Tests.Fixtures.SchemaIdentity.Second.Event"/> but a different namespace.</summary>
    public sealed class Event
    {
        public string? FirstOnly { get; set; }
    }
}

namespace Bielu.AspNetCore.AsyncApi.Tests.Fixtures.SchemaIdentity.Second
{
    /// <summary>Payload with the same short type name as <see cref="Bielu.AspNetCore.AsyncApi.Tests.Fixtures.SchemaIdentity.First.Event"/> but a different namespace.</summary>
    public sealed class Event
    {
        public int SecondOnly { get; set; }
    }
}

namespace Bielu.AspNetCore.AsyncApi.Tests.Fixtures.SchemaIdentity
{
    public static class SchemaIdentityDocuments
    {
        public const string TwoNamespaceCollision = "schema-identity-two-namespaces";
        public const string TwoNamespaceResolved = "schema-identity-two-namespaces-resolved";
        public const string DeliberateReuse = "schema-identity-deliberate-reuse";
        public const string Generics = "schema-identity-generics";
        public const string SanitizeCollision = "schema-identity-sanitize-collision";
        public const string CustomReferenceId = "schema-identity-custom-id";
        public const string InlineSchema = "schema-identity-inline";
        public const string OperationIdCollision = "schema-identity-operation-id-collision";
        public const string MessageIdCollision = "schema-identity-message-id-collision";
    }

    /// <summary>
    /// Reproduces the reported defect: two unrelated payload types that share a short type name ("Event") but
    /// live in different namespaces, each assigned to its own channel with an explicit, distinct MessageId.
    /// </summary>
    [AsyncApi(SchemaIdentityDocuments.TwoNamespaceCollision)]
    public sealed class TwoNamespaceCollisionChannels
    {
        [Channel("schema-identity/first")]
        [Message(typeof(First.Event), MessageId = "firstEvent")]
        [SubscribeOperation(OperationId = "firstEventOperation")]
        public void FirstEvent()
        {
        }

        [Channel("schema-identity/second")]
        [Message(typeof(Second.Event), MessageId = "secondEvent")]
        [SubscribeOperation(OperationId = "secondEventOperation")]
        public void SecondEvent()
        {
        }
    }

    /// <summary>
    /// Same two colliding types as <see cref="TwoNamespaceCollisionChannels"/>, but registered under a
    /// separate document name so a test can configure a custom <c>CreateSchemaReferenceId</c> that
    /// disambiguates them (for example by including the namespace) instead of hitting the collision.
    /// </summary>
    [AsyncApi(SchemaIdentityDocuments.TwoNamespaceResolved)]
    public sealed class TwoNamespaceCollisionResolvedChannels
    {
        [Channel("schema-identity-resolved/first")]
        [Message(typeof(First.Event), MessageId = "firstEvent")]
        [SubscribeOperation(OperationId = "firstEventOperation")]
        public void FirstEvent()
        {
        }

        [Channel("schema-identity-resolved/second")]
        [Message(typeof(Second.Event), MessageId = "secondEvent")]
        [SubscribeOperation(OperationId = "secondEventOperation")]
        public void SecondEvent()
        {
        }
    }

    /// <summary>Payload deliberately referenced from two different channels; must map to a single component.</summary>
    public sealed class SharedNotification
    {
        public string Message { get; set; } = string.Empty;
    }

    [AsyncApi(SchemaIdentityDocuments.DeliberateReuse)]
    public sealed class DeliberateReuseChannels
    {
        [Channel("schema-identity/reuse-a")]
        [Message(typeof(SharedNotification))]
        [SubscribeOperation(OperationId = "deliberateReuseOperationA")]
        public void NotifyA()
        {
        }

        [Channel("schema-identity/reuse-b")]
        [Message(typeof(SharedNotification))]
        [SubscribeOperation(OperationId = "deliberateReuseOperationB")]
        public void NotifyB()
        {
        }
    }

    /// <summary>Generic wrapper used with two different closed generic type arguments.</summary>
    public sealed class Envelope<T>
    {
        public T? Body { get; set; }

        public string? Kind { get; set; }
    }

    public sealed class FirstEnvelopePayload
    {
        public string? FirstOnly { get; set; }
    }

    public sealed class SecondEnvelopePayload
    {
        public string? SecondOnly { get; set; }
    }

    [AsyncApi(SchemaIdentityDocuments.Generics)]
    public sealed class GenericPayloadChannels
    {
        [Channel("schema-identity/generic-first")]
        [Message(typeof(Envelope<FirstEnvelopePayload>))]
        [SubscribeOperation(OperationId = "genericFirstOperation")]
        public void First()
        {
        }

        [Channel("schema-identity/generic-second")]
        [Message(typeof(Envelope<SecondEnvelopePayload>))]
        [SubscribeOperation(OperationId = "genericSecondOperation")]
        public void Second()
        {
        }
    }

    /// <summary>Two distinct types whose custom reference ids collide only after sanitization/camelCasing.</summary>
    public sealed class SanitizeCollisionTypeA
    {
        public string? A { get; set; }
    }

    public sealed class SanitizeCollisionTypeB
    {
        public string? B { get; set; }
    }

    [AsyncApi(SchemaIdentityDocuments.SanitizeCollision)]
    public sealed class SanitizeCollisionChannels
    {
        [Channel("schema-identity/sanitize-a")]
        [Message(typeof(SanitizeCollisionTypeA))]
        [SubscribeOperation(OperationId = "sanitizeCollisionOperationA")]
        public void OperationA()
        {
        }

        [Channel("schema-identity/sanitize-b")]
        [Message(typeof(SanitizeCollisionTypeB))]
        [SubscribeOperation(OperationId = "sanitizeCollisionOperationB")]
        public void OperationB()
        {
        }
    }

    public sealed class CustomIdPayload
    {
        public string? Value { get; set; }
    }

    [AsyncApi(SchemaIdentityDocuments.CustomReferenceId)]
    public sealed class CustomReferenceIdChannels
    {
        [Channel("schema-identity/custom-id")]
        [Message(typeof(CustomIdPayload))]
        [SubscribeOperation(OperationId = "customReferenceIdOperation")]
        public void Operation()
        {
        }
    }

    public sealed class InlinePayload
    {
        public string? Value { get; set; }
    }

    [AsyncApi(SchemaIdentityDocuments.InlineSchema)]
    public sealed class InlineSchemaChannels
    {
        [Channel("schema-identity/inline")]
        [Message(typeof(InlinePayload))]
        [SubscribeOperation(OperationId = "inlineSchemaOperation")]
        public void Operation()
        {
        }
    }

    public sealed class OperationIdPayloadA
    {
        public string? A { get; set; }
    }

    public sealed class OperationIdPayloadB
    {
        public string? B { get; set; }
    }

    /// <summary>Two genuinely different operations explicitly configured with the same OperationId.</summary>
    [AsyncApi(SchemaIdentityDocuments.OperationIdCollision)]
    public sealed class OperationIdCollisionChannels
    {
        [Channel("schema-identity/op-a")]
        [Message(typeof(OperationIdPayloadA))]
        [SubscribeOperation(OperationId = "duplicateOperationId")]
        public void OperationA()
        {
        }

        [Channel("schema-identity/op-b")]
        [Message(typeof(OperationIdPayloadB))]
        [SubscribeOperation(OperationId = "duplicateOperationId")]
        public void OperationB()
        {
        }
    }

    public sealed class MessageIdPayloadA
    {
        public string? A { get; set; }
    }

    public sealed class MessageIdPayloadB
    {
        public string? B { get; set; }
    }

    /// <summary>Two genuinely different messages (different payload types) explicitly sharing a MessageId.</summary>
    [AsyncApi(SchemaIdentityDocuments.MessageIdCollision)]
    public sealed class MessageIdCollisionChannels
    {
        [Channel("schema-identity/msg-a")]
        [Message(typeof(MessageIdPayloadA), MessageId = "duplicateMessageId")]
        [SubscribeOperation(OperationId = "messageIdCollisionOperationA")]
        public void MessageA()
        {
        }

        [Channel("schema-identity/msg-b")]
        [Message(typeof(MessageIdPayloadB), MessageId = "duplicateMessageId")]
        [SubscribeOperation(OperationId = "messageIdCollisionOperationB")]
        public void MessageB()
        {
        }
    }
}
