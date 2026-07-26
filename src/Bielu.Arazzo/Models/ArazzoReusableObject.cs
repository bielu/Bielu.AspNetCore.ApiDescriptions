using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.10 Reusable Object: references an entry in <see cref="ArazzoComponents"/> by runtime expression.</summary>
public sealed class ArazzoReusableObject : IArazzoSerializable
{
    /// <summary>Runtime expression pointing at the referenced object, e.g. <c>$components.successActions.notify</c>.</summary>
    public required string Reference { get; set; }

    /// <summary>Only applicable when the reference targets a Parameter Object.</summary>
    public string? Value { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("reference");
        writer.WriteValue(Reference);
        writer.WriteOptionalProperty("value", Value);
        writer.WriteEndObject();
    }
}

/// <summary>
/// The <c>T | Reusable Object</c> shape used for parameters, success actions, and failure actions at both
/// the workflow and step level. Exactly one of <see cref="Value"/> or <see cref="Reference"/> is set.
/// </summary>
public sealed class ArazzoReferenceable<T> : IArazzoSerializable
    where T : IArazzoSerializable
{
    public T? Value { get; set; }

    public ArazzoReusableObject? Reference { get; set; }

    public bool IsReference => Reference is not null;

    public static ArazzoReferenceable<T> Of(T value) => new() { Value = value };

    public static ArazzoReferenceable<T> Of(ArazzoReusableObject reference) => new() { Reference = reference };

    public void SerializeAsV1(IArazzoWriter writer)
    {
        if (Reference is not null)
        {
            Reference.SerializeAsV1(writer);
            return;
        }

        Value!.SerializeAsV1(writer);
    }
}
