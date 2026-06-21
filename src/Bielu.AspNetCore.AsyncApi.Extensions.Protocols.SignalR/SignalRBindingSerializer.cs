using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;

/// <summary>
/// Small serialization helpers shared by the SignalR bindings.
/// </summary>
internal static class SignalRBindingSerializer
{
    /// <summary>Serializes a schema using the writer matching the requested AsyncAPI spec version.</summary>
    public static void WriteSchema(IAsyncApiWriter writer, AsyncApiJsonSchema schema, bool useV2Schema)
    {
        if (useV2Schema)
        {
            schema.SerializeV2(writer);
        }
        else
        {
            schema.SerializeV3(writer);
        }
    }
}
