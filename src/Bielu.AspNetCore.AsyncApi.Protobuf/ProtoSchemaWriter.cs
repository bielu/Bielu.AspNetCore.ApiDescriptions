using System.Diagnostics.CodeAnalysis;
using System.Text;
using Google.Protobuf.Reflection;

namespace Bielu.AspNetCore.AsyncApi.Protobuf;

/// <summary>
/// Renders the <c>.proto</c> source of a message: one self-contained file with the message and every message and enum
/// it uses, the way AsyncAPI's Protobuf schema format carries a payload.
/// </summary>
/// <remarks>
/// AsyncAPI tooling reads a single <c>.proto</c> string per payload and takes as its payload the message no other
/// message uses, or the one marked <c>@RootNode</c>, which this marks. Types from the well-known
/// <c>google/protobuf</c> and <c>google/type</c> files stay imports, as those parsers expect. Every other type the
/// message uses is declared in the same file, under the message's package; field numbers, types, labels and defaults
/// are those of the descriptor, so the source describes the same wire format.
/// </remarks>
internal static class ProtoSchemaWriter
{
    /// <summary>The comment that marks the payload message for AsyncAPI's Protobuf parser.</summary>
    internal const string RootNodeMarker = "// @RootNode";

    /// <summary>
    /// Renders <paramref name="root"/>'s source, unless it can't be rendered faithfully: a message from an editions
    /// file, which AsyncAPI's Protobuf schema format has no version for, or one that uses a type from a file of another
    /// syntax, whose labels and defaults would change meaning once declared in the root's file.
    /// </summary>
    public static bool TryWrite(MessageDescriptor root, [NotNullWhen(true)] out string? source)
    {
        source = null;
        var syntax = Syntax(root.File);
        if (syntax == "editions")
        {
            return false;
        }

        var imports = new SortedSet<string>(StringComparer.Ordinal);
        var names = new Dictionary<IDescriptor, string>();
        var declarations = CollectDeclarations(root, imports, names);
        if (declarations.Any(declaration => Syntax(declaration.File) != syntax))
        {
            return false;
        }

        var proto3 = syntax == "proto3";
        var builder = new StringBuilder();
        builder.Append(proto3 ? "syntax = \"proto3\";" : "syntax = \"proto2\";").Append('\n');

        if (!string.IsNullOrEmpty(root.File.Package))
        {
            builder.Append('\n').Append("package ").Append(root.File.Package).Append(";\n");
        }

        if (imports.Count > 0)
        {
            builder.Append('\n');
            foreach (var import in imports)
            {
                builder.Append("import \"").Append(import).Append("\";\n");
            }
        }

        foreach (var declaration in declarations)
        {
            builder.Append('\n');
            if (declaration is MessageDescriptor message)
            {
                WriteMessage(builder, message, names[message], root, names, proto3, depth: 0);
            }
            else
            {
                WriteEnum(builder, (EnumDescriptor)declaration, names[declaration], depth: 0);
            }
        }

        source = builder.ToString();
        return true;
    }

    /// <summary>
    /// The Protobuf version of <paramref name="message"/>'s file for the <c>schemaFormat</c>: <c>3</c> for proto3 and
    /// <c>2</c> for proto2.
    /// </summary>
    public static string SyntaxVersion(MessageDescriptor message) => Syntax(message.File) == "proto3" ? "3" : "2";

    /// <summary>The file's syntax: <c>proto2</c>, <c>proto3</c> or <c>editions</c>.</summary>
    private static string Syntax(FileDescriptor file) =>
        file.ToProto().Syntax is { Length: > 0 } syntax ? syntax : "proto2";

    /// <summary>
    /// The top-level messages and enums to declare, starting with the one holding <paramref name="root"/> and followed
    /// by those its fields use, transitively, in the order they are found. <paramref name="names"/> receives the name
    /// each one is declared under.
    /// </summary>
    private static List<IDescriptor> CollectDeclarations(MessageDescriptor root, SortedSet<string> imports,
        Dictionary<IDescriptor, string> names)
    {
        var declarations = new List<IDescriptor>();
        var takenNames = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<IDescriptor>();
        Enqueue(TopLevel(root));

        while (pending.TryDequeue(out var declaration))
        {
            declarations.Add(declaration);
            if (declaration is MessageDescriptor message)
            {
                foreach (var field in AllFields(message))
                {
                    if (field.FieldType is FieldType.Message or FieldType.Group)
                    {
                        Use(field.MessageType, field.MessageType.File);
                    }
                    else if (field.FieldType == FieldType.Enum)
                    {
                        Use(field.EnumType, field.EnumType.File);
                    }
                }
            }
        }

        return declarations;

        void Use(IDescriptor type, FileDescriptor file)
        {
            if (IsImported(file))
            {
                imports.Add(file.Name);
            }
            else
            {
                Enqueue(TopLevel(type));
            }
        }

        void Enqueue(IDescriptor declaration)
        {
            if (names.ContainsKey(declaration))
            {
                return;
            }

            // Every type is declared under the payload's package, so a type from another package whose name is
            // already taken is renamed after its package. Type names don't travel on the wire; field numbers do.
            var name = declaration.Name;
            if (!takenNames.Add(name))
            {
                name = declaration.FullName.Replace('.', '_');
                while (!takenNames.Add(name))
                {
                    name += "_";
                }
            }

            names.Add(declaration, name);
            pending.Enqueue(declaration);
        }
    }

    private static IEnumerable<FieldDescriptor> AllFields(MessageDescriptor message) =>
        message.Fields.InDeclarationOrder().Concat(message.NestedTypes.SelectMany(AllFields));

    private static IDescriptor TopLevel(IDescriptor type)
    {
        var containing = type switch
        {
            MessageDescriptor message => message.ContainingType,
            EnumDescriptor enumType => enumType.ContainingType,
            _ => null,
        };
        return containing is null ? type : TopLevel(containing);
    }

    private static bool IsImported(FileDescriptor file) =>
        file.Name.StartsWith("google/protobuf/", StringComparison.Ordinal)
        || file.Name.StartsWith("google/type/", StringComparison.Ordinal);

    private static void WriteMessage(StringBuilder builder, MessageDescriptor message, string name,
        MessageDescriptor root, IReadOnlyDictionary<IDescriptor, string> names, bool proto3, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (message == root)
        {
            builder.Append(indent).Append(RootNodeMarker).Append('\n');
        }

        builder.Append(indent).Append("message ").Append(name).Append(" {\n");

        foreach (var nestedEnum in message.EnumTypes)
        {
            WriteEnum(builder, nestedEnum, nestedEnum.Name, depth + 1);
        }

        foreach (var nested in message.NestedTypes.Where(nested => nested.ToProto().Options?.MapEntry != true))
        {
            WriteMessage(builder, nested, nested.Name, root, names, proto3, depth + 1);
        }

        var fieldIndent = new string(' ', (depth + 1) * 2);
        var writtenOneofs = new HashSet<OneofDescriptor>();
        foreach (var field in message.Fields.InDeclarationOrder())
        {
            if (field.RealContainingOneof is { } oneof)
            {
                if (writtenOneofs.Add(oneof))
                {
                    builder.Append(fieldIndent).Append("oneof ").Append(oneof.Name).Append(" {\n");
                    foreach (var member in oneof.Fields)
                    {
                        WriteField(builder, member, names, proto3, depth + 2, inOneof: true);
                    }

                    builder.Append(fieldIndent).Append("}\n");
                }

                continue;
            }

            WriteField(builder, field, names, proto3, depth + 1, inOneof: false);
        }

        var proto = message.ToProto();
        foreach (var range in proto.ReservedRange)
        {
            builder.Append(fieldIndent).Append("reserved ").Append(range.Start);
            if (range.End - 1 > range.Start)
            {
                builder.Append(" to ").Append(range.End - 1);
            }

            builder.Append(";\n");
        }

        foreach (var reservedName in proto.ReservedName)
        {
            builder.Append(fieldIndent).Append("reserved \"").Append(reservedName).Append("\";\n");
        }

        builder.Append(indent).Append("}\n");
    }

    private static void WriteField(StringBuilder builder, FieldDescriptor field,
        IReadOnlyDictionary<IDescriptor, string> names, bool proto3, int depth, bool inOneof)
    {
        var proto = field.ToProto();
        builder.Append(' ', depth * 2);
        if (field.IsMap)
        {
            var key = field.MessageType.FindFieldByNumber(1);
            var value = field.MessageType.FindFieldByNumber(2);
            builder.Append("map<").Append(TypeName(key, names)).Append(", ").Append(TypeName(value, names)).Append("> ");
        }
        else
        {
            if (field.IsRepeated)
            {
                builder.Append("repeated ");
            }
            else if (!inOneof && (proto3 ? proto.Proto3Optional : !field.IsRequired))
            {
                builder.Append("optional ");
            }
            else if (!proto3 && field.IsRequired)
            {
                builder.Append("required ");
            }

            builder.Append(TypeName(field, names)).Append(' ');
        }

        builder.Append(field.Name).Append(" = ").Append(field.FieldNumber);

        // Only proto2 declares defaults. The descriptor holds a string default unescaped and a bytes one already C-escaped.
        if (proto.HasDefaultValue)
        {
            builder.Append(" [default = ");
            switch (field.FieldType)
            {
                case FieldType.String:
                    AppendQuoted(builder, proto.DefaultValue);
                    break;
                case FieldType.Bytes:
                    builder.Append('"').Append(proto.DefaultValue).Append('"');
                    break;
                default:
                    builder.Append(proto.DefaultValue);
                    break;
            }

            builder.Append(']');
        }

        builder.Append(";\n");
    }

    private static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                case < ' ' or '\x7f':
                    builder.Append('\\').Append(Convert.ToString(character, 8).PadLeft(3, '0'));
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        builder.Append('"');
    }

    private static void WriteEnum(StringBuilder builder, EnumDescriptor enumType, string name, int depth)
    {
        var indent = new string(' ', depth * 2);
        builder.Append(indent).Append("enum ").Append(name).Append(" {\n");
        foreach (var value in enumType.Values)
        {
            builder.Append(indent).Append("  ").Append(value.Name).Append(" = ").Append(value.Number).Append(";\n");
        }

        builder.Append(indent).Append("}\n");
    }

    private static string TypeName(FieldDescriptor field, IReadOnlyDictionary<IDescriptor, string> names) =>
        field.FieldType switch
        {
            FieldType.Double => "double",
            FieldType.Float => "float",
            FieldType.Int64 => "int64",
            FieldType.UInt64 => "uint64",
            FieldType.Int32 => "int32",
            FieldType.Fixed64 => "fixed64",
            FieldType.Fixed32 => "fixed32",
            FieldType.Bool => "bool",
            FieldType.String => "string",
            FieldType.Bytes => "bytes",
            FieldType.UInt32 => "uint32",
            FieldType.SFixed32 => "sfixed32",
            FieldType.SFixed64 => "sfixed64",
            FieldType.SInt32 => "sint32",
            FieldType.SInt64 => "sint64",
            FieldType.Enum => Reference(field.EnumType, field.EnumType.File, names),
            _ => Reference(field.MessageType, field.MessageType.File, names),
        };

    /// <summary>
    /// How a field refers to a type: an imported one by its fully qualified name, any other by the name this file
    /// declares its top-level type under, followed by its path within that type.
    /// </summary>
    private static string Reference(IDescriptor type, FileDescriptor file, IReadOnlyDictionary<IDescriptor, string> names)
    {
        if (IsImported(file))
        {
            return "." + type.FullName;
        }

        var topLevel = TopLevel(type);
        return names[topLevel] + type.FullName[topLevel.FullName.Length..];
    }
}
