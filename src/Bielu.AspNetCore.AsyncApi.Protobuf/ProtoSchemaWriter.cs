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
/// message uses is declared in the same file, under the message's package; field numbers, types and labels are
/// those of the descriptor, so the source describes the same wire format.
/// </remarks>
internal static class ProtoSchemaWriter
{
    /// <summary>The comment that marks the payload message for AsyncAPI's Protobuf parser.</summary>
    internal const string RootNodeMarker = "// @RootNode";

    public static string Write(MessageDescriptor root)
    {
        var imports = new SortedSet<string>(StringComparer.Ordinal);
        var declarations = CollectDeclarations(root, imports);
        var file = root.File.ToProto();

        var builder = new StringBuilder();
        builder.Append(file.Syntax switch
        {
            "proto3" => "syntax = \"proto3\";",
            "editions" => $"edition = \"{file.Edition.ToString().Replace("Edition", string.Empty, StringComparison.Ordinal)}\";",
            _ => "syntax = \"proto2\";",
        }).Append('\n');

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

        var proto3 = file.Syntax == "proto3";
        foreach (var declaration in declarations)
        {
            builder.Append('\n');
            if (declaration is MessageDescriptor message)
            {
                WriteMessage(builder, message, root, proto3, depth: 0);
            }
            else
            {
                WriteEnum(builder, (EnumDescriptor)declaration, depth: 0);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The Protobuf version of <paramref name="message"/>'s file for the <c>schemaFormat</c>: <c>2</c> for proto2, and
    /// <c>3</c> for proto3 and for editions, which AsyncAPI has no version of its own for.
    /// </summary>
    public static string SyntaxVersion(MessageDescriptor message) =>
        message.File.ToProto().Syntax is "proto3" or "editions" ? "3" : "2";

    /// <summary>
    /// The top-level messages and enums to declare, starting with the one holding <paramref name="root"/> and followed
    /// by those its fields use, transitively, in the order they are found.
    /// </summary>
    private static List<IDescriptor> CollectDeclarations(MessageDescriptor root, SortedSet<string> imports)
    {
        var declarations = new List<IDescriptor>();
        var declaredNames = new HashSet<string>(StringComparer.Ordinal);
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
            // Types from other packages are declared under the payload's package, so two of them with the same
            // name would clash; the first one found is kept.
            if (declaredNames.Add(RelativeName(declaration)))
            {
                pending.Enqueue(declaration);
            }
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

    private static void WriteMessage(StringBuilder builder, MessageDescriptor message, MessageDescriptor root, bool proto3, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (message == root)
        {
            builder.Append(indent).Append(RootNodeMarker).Append('\n');
        }

        builder.Append(indent).Append("message ").Append(message.Name).Append(" {\n");

        foreach (var nestedEnum in message.EnumTypes)
        {
            WriteEnum(builder, nestedEnum, depth + 1);
        }

        foreach (var nested in message.NestedTypes.Where(nested => nested.ToProto().Options?.MapEntry != true))
        {
            WriteMessage(builder, nested, root, proto3, depth + 1);
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
                        WriteField(builder, member, proto3, depth + 2, inOneof: true);
                    }

                    builder.Append(fieldIndent).Append("}\n");
                }

                continue;
            }

            WriteField(builder, field, proto3, depth + 1, inOneof: false);
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

        foreach (var name in proto.ReservedName)
        {
            builder.Append(fieldIndent).Append("reserved \"").Append(name).Append("\";\n");
        }

        builder.Append(indent).Append("}\n");
    }

    private static void WriteField(StringBuilder builder, FieldDescriptor field, bool proto3, int depth, bool inOneof)
    {
        builder.Append(' ', depth * 2);
        if (field.IsMap)
        {
            var key = field.MessageType.FindFieldByNumber(1);
            var value = field.MessageType.FindFieldByNumber(2);
            builder.Append("map<").Append(TypeName(key)).Append(", ").Append(TypeName(value)).Append("> ");
        }
        else
        {
            if (field.IsRepeated)
            {
                builder.Append("repeated ");
            }
            else if (!inOneof && (proto3 ? field.ToProto().Proto3Optional : !field.IsRequired))
            {
                builder.Append("optional ");
            }
            else if (!proto3 && field.IsRequired)
            {
                builder.Append("required ");
            }

            builder.Append(TypeName(field)).Append(' ');
        }

        builder.Append(field.Name).Append(" = ").Append(field.FieldNumber).Append(";\n");
    }

    private static void WriteEnum(StringBuilder builder, EnumDescriptor enumType, int depth)
    {
        var indent = new string(' ', depth * 2);
        builder.Append(indent).Append("enum ").Append(enumType.Name).Append(" {\n");
        foreach (var value in enumType.Values)
        {
            builder.Append(indent).Append("  ").Append(value.Name).Append(" = ").Append(value.Number).Append(";\n");
        }

        builder.Append(indent).Append("}\n");
    }

    private static string TypeName(FieldDescriptor field) => field.FieldType switch
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
        FieldType.Enum => Reference(field.EnumType, field.EnumType.File),
        _ => Reference(field.MessageType, field.MessageType.File),
    };

    /// <summary>
    /// How a field refers to a type: an imported one by its fully qualified name, any other by its name within its
    /// package, which is where this file declares it.
    /// </summary>
    private static string Reference(IDescriptor type, FileDescriptor file) =>
        IsImported(file) ? "." + type.FullName : RelativeName(type);

    private static string RelativeName(IDescriptor type)
    {
        var package = type.File.Package;
        return string.IsNullOrEmpty(package) ? type.FullName : type.FullName[(package.Length + 1)..];
    }
}
