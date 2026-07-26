using System.Text.Json.Nodes;

namespace Bielu.Arazzo.Writers;

/// <summary>
/// Builds a <see cref="JsonNode"/> tree from the <see cref="IArazzoWriter"/> primitive calls a model's
/// <c>SerializeAsV1</c> makes. Both <see cref="ArazzoJsonWriter"/> and <see cref="ArazzoYamlWriter"/> run
/// serialization through this once and then render the resulting tree to their target text format — so
/// format-specific rendering never has to reason about deferred "is this property a scalar or a nested
/// block" decisions; by the time rendering starts, the whole shape is already known.
/// </summary>
internal sealed class ArazzoJsonNodeWriter : IArazzoWriter
{
    private readonly Stack<JsonNode> _stack = new();
    private string? _pendingPropertyName;

    public JsonNode? Result { get; private set; }

    public void WriteStartObject() => Push(new JsonObject());

    public void WriteStartArray() => Push(new JsonArray());

    public void WriteEndObject() => Pop();

    public void WriteEndArray() => Pop();

    public void WritePropertyName(string name) => _pendingPropertyName = name;

    public void WriteValue(string? value) => AttachToParent(JsonValue.Create(value));

    public void WriteValue(double value) => AttachToParent(JsonValue.Create(value));

    public void WriteValue(bool value) => AttachToParent(JsonValue.Create(value));

    public void WriteValue(int value) => AttachToParent(JsonValue.Create(value));

    public void WriteNull() => AttachToParent(null);

    public void WriteRaw(JsonNode? node) => AttachToParent(node?.DeepClone());

    private void Push(JsonNode node)
    {
        AttachToParent(node);
        _stack.Push(node);
    }

    private void Pop()
    {
        var node = _stack.Pop();
        if (_stack.Count == 0)
        {
            Result = node;
        }
    }

    private void AttachToParent(JsonNode? value)
    {
        if (_stack.Count == 0)
        {
            Result = value;
            return;
        }

        switch (_stack.Peek())
        {
            case JsonObject obj:
                var name = _pendingPropertyName
                    ?? throw new InvalidOperationException("WritePropertyName must precede a value inside an object.");
                obj[name] = value;
                _pendingPropertyName = null;
                break;
            case JsonArray array:
                array.Add(value);
                break;
        }
    }
}
