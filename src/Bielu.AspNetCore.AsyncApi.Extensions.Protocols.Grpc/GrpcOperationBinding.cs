using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;

/// <summary>
/// Operation binding for the <c>grpc</c> protocol. An operation maps to a single RPC method on a
/// service.
/// </summary>
public class GrpcOperationBinding : OperationBinding<GrpcOperationBinding>
{
    /// <summary>The RPC method name, for example <c>SayHello</c>.</summary>
    public string? Method { get; set; }

    /// <summary>The kind of RPC (see <see cref="GrpcMethodType"/>).</summary>
    public GrpcMethodType? MethodType { get; set; }

    /// <summary>The fully-qualified protobuf type of the request message.</summary>
    public string? RequestType { get; set; }

    /// <summary>The fully-qualified protobuf type of the response message.</summary>
    public string? ResponseType { get; set; }

    /// <summary>The declared idempotency level (see <see cref="GrpcProtocol.IdempotencyLevels"/>).</summary>
    public string? IdempotencyLevel { get; set; }

    /// <summary>The call deadline in seconds, when the operation declares one.</summary>
    public double? DeadlineSeconds { get; set; }

    /// <inheritdoc />
    public override string BindingKey => GrpcProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<GrpcOperationBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "method", (a, n) => a.Method = n.GetScalarValue() },
        { "methodType", (a, n) => a.MethodType = GrpcMethodTypeExtensions.Parse(n.GetScalarValue()) },
        { "requestType", (a, n) => a.RequestType = n.GetScalarValue() },
        { "responseType", (a, n) => a.ResponseType = n.GetScalarValue() },
        { "idempotencyLevel", (a, n) => a.IdempotencyLevel = n.GetScalarValue() },
        { "deadlineSeconds", (a, n) => a.DeadlineSeconds = ParseDeadline(n.GetScalarValue()) },
    };

    /// <inheritdoc />
    public override void SerializeV2(IAsyncApiWriter writer) => Serialize(writer);

    /// <inheritdoc />
    public override void SerializeV3(IAsyncApiWriter writer) => Serialize(writer);

    /// <inheritdoc />
    public override void SerializeProperties(IAsyncApiWriter writer) => Serialize(writer);

    private void Serialize(IAsyncApiWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteOptionalProperty("method", Method);
        writer.WriteOptionalProperty("methodType", MethodType?.ToWireName());
        writer.WriteOptionalProperty("requestType", RequestType);
        writer.WriteOptionalProperty("responseType", ResponseType);
        writer.WriteOptionalProperty("idempotencyLevel", IdempotencyLevel);
        writer.WriteOptionalProperty("deadlineSeconds", DeadlineSeconds);
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? GrpcProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }

    private static double? ParseDeadline(string? value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
