using System.Text.Json.Nodes;

namespace Bielu.Overlay;

/// <summary>
/// Parent-relative edits on a <see cref="JsonNode"/> tree. JSONPath hands back the matched node itself,
/// but removing or replacing it requires reaching its container — which <see cref="JsonNode.Parent"/>
/// provides, and from which the key or index can be recovered.
/// </summary>
internal static class JsonNodeEditor
{
    /// <summary>Removes <paramref name="node"/> from its containing object or array.</summary>
    /// <returns><see langword="false"/> if the node is a tree root, or is not actually present in its parent.</returns>
    public static bool TryRemove(JsonNode node)
    {
        switch (node.Parent)
        {
            case JsonArray array:
            {
                // Index is resolved against the live array rather than cached, so removing several
                // matches from the same array stays correct regardless of the order they come back in.
                var index = array.IndexOf(node);
                if (index < 0)
                {
                    return false;
                }

                array.RemoveAt(index);
                return true;
            }

            case JsonObject obj:
            {
                if (TryFindKey(obj, node) is not { } key)
                {
                    return false;
                }

                obj.Remove(key);
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Replaces <paramref name="node"/> within its containing object or array with <paramref name="replacement"/>.</summary>
    /// <returns><see langword="false"/> if the node is a tree root, or is not actually present in its parent.</returns>
    public static bool TryReplace(JsonNode node, JsonNode? replacement)
    {
        switch (node.Parent)
        {
            case JsonArray array:
            {
                var index = array.IndexOf(node);
                if (index < 0)
                {
                    return false;
                }

                array[index] = replacement;
                return true;
            }

            case JsonObject obj:
            {
                if (TryFindKey(obj, node) is not { } key)
                {
                    return false;
                }

                obj[key] = replacement;
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Finds the property name under which <paramref name="node"/> is stored in <paramref name="obj"/>.
    /// Matched by reference: two properties can hold equal values, and only identity identifies the one
    /// JSONPath actually selected.
    /// </summary>
    private static string? TryFindKey(JsonObject obj, JsonNode node)
    {
        foreach (var (key, value) in obj)
        {
            if (ReferenceEquals(value, node))
            {
                return key;
            }
        }

        return null;
    }
}
