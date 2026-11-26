using System.Text;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>Writes a <see cref="LikeC4Model"/> as LikeC4 DSL.</summary>
internal static class LikeC4Writer
{
    private const string Indent = "  ";

    public static string Write(LikeC4Model model, string header)
    {
        var writer = new Writer();
        foreach (var line in header.Split('\n'))
        {
            writer.Line(0, $"// {line}".TrimEnd());
        }

        writer.Blank();
        WriteSpecification(writer, model);
        writer.Blank();
        WriteModel(writer, model);
        if (model.Views.Count > 0 || model.DynamicViews.Count > 0)
        {
            writer.Blank();
            WriteViews(writer, model);
        }

        return writer.ToString();
    }

    private static void WriteSpecification(Writer writer, LikeC4Model model)
    {
        writer.Line(0, "specification {");
        foreach (var kind in model.ElementKinds)
        {
            writer.Line(1, $"element {kind.Name} {{");
            writer.Line(2, $"notation {Quote(kind.Notation)}");
            if (kind.Shape is not null)
            {
                writer.Line(2, "style {");
                writer.Line(3, $"shape {kind.Shape}");
                writer.Line(2, "}");
            }

            writer.Line(1, "}");
        }

        foreach (var kind in model.RelationshipKinds)
        {
            writer.Line(1, $"relationship {kind.Name} {{");
            writer.Line(2, $"notation {Quote(kind.Notation)}");
            if (kind.Line is not null)
            {
                writer.Line(2, $"line {kind.Line}");
            }

            writer.Line(1, "}");
        }

        foreach (var tag in model.Tags)
        {
            writer.Line(1, $"tag {tag}");
        }

        writer.Line(0, "}");
    }

    private static void WriteModel(Writer writer, LikeC4Model model)
    {
        writer.Line(0, "model {");
        var first = true;
        foreach (var element in model.Elements)
        {
            if (!first)
            {
                writer.Blank();
            }

            first = false;
            writer.Line(1, $"{element.Id} = {element.Kind} {Quote(element.Title)} {{");
            WriteTags(writer, element.Tags);
            WriteDescription(writer, element.Description);
            WriteTechnology(writer, element.Technology);
            foreach (var link in element.Links)
            {
                writer.Line(2, link.Title is null ? $"link {link.Url}" : $"link {link.Url} {Quote(link.Title)}");
            }

            WriteMetadata(writer, element.Metadata);
            writer.Line(1, "}");
        }

        if (model.Relationships.Count > 0)
        {
            writer.Blank();
        }

        foreach (var relationship in model.Relationships)
        {
            writer.Line(1, $"{relationship.Source} -[{relationship.Kind}]-> {relationship.Target} {Quote(relationship.Title)} {{");
            WriteTags(writer, relationship.Tags);
            WriteDescription(writer, relationship.Description);
            WriteTechnology(writer, relationship.Technology);
            WriteMetadata(writer, relationship.Metadata);
            writer.Line(1, "}");
        }

        writer.Line(0, "}");
    }

    private static void WriteViews(Writer writer, LikeC4Model model)
    {
        writer.Line(0, "views {");
        var first = true;
        foreach (var view in model.Views)
        {
            if (!first)
            {
                writer.Blank();
            }

            first = false;
            writer.Line(1, view.Of is null ? $"view {view.Id} {{" : $"view {view.Id} of {view.Of} {{");
            writer.Line(2, $"title {Quote(view.Title)}");
            WriteDescription(writer, view.Description);
            writer.Line(2, $"include {string.Join(", ", view.Include)}");
            foreach (var exclude in view.Exclude)
            {
                writer.Line(2, $"exclude {exclude}");
            }

            writer.Line(1, "}");
        }

        foreach (var view in model.DynamicViews)
        {
            if (!first)
            {
                writer.Blank();
            }

            first = false;
            writer.Line(1, $"dynamic view {view.Id} {{");
            writer.Line(2, $"title {Quote(view.Title)}");
            WriteDescription(writer, view.Description);
            writer.Line(2, "variant sequence");
            foreach (var step in view.Steps)
            {
                if (step.Notes is null && step.NavigateTo is null)
                {
                    writer.Line(2, $"{step.Source} -> {step.Target} {Quote(step.Title)}");
                    continue;
                }

                writer.Line(2, $"{step.Source} -> {step.Target} {Quote(step.Title)} {{");
                WriteText(writer, 3, "notes", step.Notes);
                if (step.NavigateTo is not null)
                {
                    writer.Line(3, $"navigateTo {step.NavigateTo}");
                }

                writer.Line(2, "}");
            }

            writer.Line(1, "}");
        }

        writer.Line(0, "}");
    }

    private static void WriteTags(Writer writer, IReadOnlyList<string> tags)
    {
        if (tags.Count > 0)
        {
            writer.Line(2, string.Join(", ", tags.Select(tag => $"#{tag}")));
        }
    }

    private static void WriteTechnology(Writer writer, string? technology)
    {
        if (technology is not null)
        {
            writer.Line(2, $"technology {Quote(technology)}");
        }
    }

    private static void WriteDescription(Writer writer, string? description) => WriteText(writer, 2, "description", description);

    /// <summary>
    /// Writes a description or notes as a triple-quoted string, which LikeC4 renders as Markdown, the way AsyncAPI
    /// and Arazzo descriptions are written. LikeC4 strips the indentation the lines share, so they are indented with
    /// the block.
    /// </summary>
    private static void WriteText(Writer writer, int depth, string property, string? text)
    {
        if (text is null)
        {
            return;
        }

        writer.Line(depth, $"{property} '''");
        foreach (var line in Normalize(text).Split('\n'))
        {
            writer.Line(line.Length == 0 ? 0 : depth + 1, Escape(line));
        }

        writer.Line(depth, "'''");
    }

    private static void WriteMetadata(Writer writer, IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        if (metadata.Count == 0)
        {
            return;
        }

        writer.Line(2, "metadata {");
        foreach (var (key, value) in metadata)
        {
            writer.Line(3, $"{key} {Quote(value)}");
        }

        writer.Line(2, "}");
    }

    /// <summary>A single-line, single-quoted LikeC4 string.</summary>
    internal static string Quote(string value) =>
        $"'{Escape(string.Join(' ', Normalize(value).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))}'";

    /// <summary>
    /// Escapes a string's quotes. LikeC4 strings read a backslash as escaping the character after it and have no
    /// way to write a backslash itself, so one is written as a slash.
    /// </summary>
    private static string Escape(string value) =>
        value.Replace('\\', '/').Replace("'", "\\'", StringComparison.Ordinal);

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim('\n');

    private sealed class Writer
    {
        private readonly StringBuilder _builder = new();

        public void Line(int depth, string text)
        {
            for (var i = 0; i < depth; i++)
            {
                _builder.Append(Indent);
            }

            _builder.Append(text).Append('\n');
        }

        public void Blank() => _builder.Append('\n');

        public override string ToString() => _builder.ToString();
    }
}
