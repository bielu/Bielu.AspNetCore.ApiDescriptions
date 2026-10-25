using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>
/// Collects, transitively, the parts of a document that some nodes refer to with <c>$ref</c>, keyed by their canonical
/// pointer. References are left as written, so each one can be looked up in <see cref="References"/> by its own text
/// (once normalized); a cycle is collected once.
/// </summary>
/// <param name="root">The whole document the references point into.</param>
internal sealed class ReferenceClosure(JsonNode root)
{
    private readonly Dictionary<string, JsonNode> _references = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _omitted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _unresolved = new(StringComparer.Ordinal);

    /// <summary>The referenced nodes in the order they were found, keyed by canonical pointer.</summary>
    public IReadOnlyList<KeyValuePair<string, JsonNode>> References => [.. _references];

    /// <summary>The references that could not be followed, with the reason.</summary>
    public IReadOnlyDictionary<string, string> Unresolved => _unresolved;

    /// <summary>What was left out of a node collected with <see cref="Include"/>, keyed by its pointer.</summary>
    public IReadOnlyDictionary<string, string> Omitted => _omitted;

    /// <summary>Collects everything <paramref name="node"/> refers to. The node itself is not collected.</summary>
    public void Walk(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject value:
                if (JsonPointer.ReferenceOf(value) is { } reference)
                {
                    Add(reference);
                }

                foreach (var (_, child) in value)
                {
                    Walk(child);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Walk(child);
                }

                break;
        }
    }

    /// <summary>Collects the node at <paramref name="reference"/> and everything it refers to.</summary>
    public void Add(string reference)
    {
        var pointer = JsonPointer.Normalize(reference);
        if (_references.ContainsKey(pointer) || _unresolved.ContainsKey(pointer))
        {
            return;
        }

        if (!JsonPointer.IsLocal(pointer))
        {
            _unresolved[pointer] = "points to another document, which is not resolved";
            return;
        }

        if (JsonPointer.Resolve(root, pointer) is not { } target)
        {
            _unresolved[pointer] = "does not exist in the document";
            return;
        }

        Collect(pointer, target.DeepClone());
    }

    /// <summary>
    /// Collects a node in place of the one at <paramref name="pointer"/>, typically a copy with parts that do not apply
    /// left out, and everything it refers to. A later reference to the pointer finds this node.
    /// </summary>
    /// <param name="pointer">The node's canonical pointer.</param>
    /// <param name="node">The node to collect. It is collected as it is, not copied.</param>
    /// <param name="omitted">What was left out of the node, said so the caller does not take it as unspecified.</param>
    public void Include(string pointer, JsonObject node, string? omitted)
    {
        if (omitted is not null)
        {
            _omitted[pointer] = omitted;
        }

        Collect(pointer, node);
    }

    private void Collect(string pointer, JsonNode node)
    {
        // Recorded before walking, so a node that refers back to itself is collected once.
        _references[pointer] = node;
        Walk(node);
    }
}
