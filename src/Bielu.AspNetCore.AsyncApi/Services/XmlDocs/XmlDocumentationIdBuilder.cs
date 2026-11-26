using System.Reflection;
using System.Text;

namespace Bielu.AspNetCore.AsyncApi.Services.XmlDocs;

internal static class XmlDocumentationIdBuilder
{
    public static string? CreateIdForMember(MemberInfo memberInfo)
    {
        // The compiler documents a generic type once, under its definition (T:Ns.Envelope`1, with `0 for its type
        // parameter in member signatures), so a constructed type or one of its members is looked up by its definition.
        if (memberInfo is Type { IsConstructedGenericType: true } constructedType)
        {
            memberInfo = constructedType.GetGenericTypeDefinition();
        }
        else if (memberInfo.DeclaringType is { IsConstructedGenericType: true } declaringType)
        {
            memberInfo = declaringType.GetGenericTypeDefinition().GetMemberWithSameMetadataDefinitionAs(memberInfo);
        }

        return memberInfo switch
        {
            Type type => CreateIdForType(type),
            MethodInfo methodInfo => CreateIdForMethod(methodInfo),
            PropertyInfo propertyInfo => CreateIdForProperty(propertyInfo),
            FieldInfo fieldInfo => CreateIdForField(fieldInfo),
            EventInfo eventInfo => CreateIdForEvent(eventInfo),
            _ => null
        };
    }

    private static string CreateIdForType(Type type)
    {
        var builder = new StringBuilder("T:");
        AppendFullTypeName(builder, type);
        return builder.ToString();
    }

    private static string CreateIdForMethod(MethodInfo methodInfo)
    {
        var builder = new StringBuilder("M:");
        AppendFullTypeName(builder, methodInfo.DeclaringType!);
        builder.Append('.').Append(methodInfo.Name);

        if (methodInfo.IsGenericMethod)
        {
            builder.Append("``").Append(methodInfo.GetGenericArguments().Length);
        }

        var parameters = methodInfo.GetParameters();
        if (parameters.Length > 0)
        {
            builder.Append('(');
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0) builder.Append(',');
                AppendParameterType(builder, parameters[i].ParameterType);
            }
            builder.Append(')');
        }

        if (methodInfo.Name is "op_Implicit" or "op_Explicit")
        {
            builder.Append('~');
            AppendParameterType(builder, methodInfo.ReturnType);
        }

        return builder.ToString();
    }

    private static string CreateIdForProperty(PropertyInfo propertyInfo)
    {
        var builder = new StringBuilder("P:");
        AppendFullTypeName(builder, propertyInfo.DeclaringType!);
        builder.Append('.').Append(propertyInfo.Name);

        var parameters = propertyInfo.GetIndexParameters();
        if (parameters.Length > 0)
        {
            builder.Append('(');
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0) builder.Append(',');
                AppendParameterType(builder, parameters[i].ParameterType);
            }
            builder.Append(')');
        }

        return builder.ToString();
    }

    private static string CreateIdForField(FieldInfo fieldInfo)
    {
        var builder = new StringBuilder("F:");
        AppendFullTypeName(builder, fieldInfo.DeclaringType!);
        builder.Append('.').Append(fieldInfo.Name);
        return builder.ToString();
    }

    private static string CreateIdForEvent(EventInfo eventInfo)
    {
        var builder = new StringBuilder("E:");
        AppendFullTypeName(builder, eventInfo.DeclaringType!);
        builder.Append('.').Append(eventInfo.Name);
        return builder.ToString();
    }

    private static void AppendFullTypeName(StringBuilder builder, Type type)
    {
        if (type.DeclaringType != null)
        {
            AppendFullTypeName(builder, type.DeclaringType);
            builder.Append('.');
        }
        else if (!string.IsNullOrEmpty(type.Namespace))
        {
            builder.Append(type.Namespace).Append('.');
        }

        // A generic type's name already carries its own arity ("Envelope`1", and "Inner`1" for a generic type
        // nested in another), which is exactly how the documentation id spells a type.
        builder.Append(type.Name);
    }

    private static void AppendParameterType(StringBuilder builder, Type type)
    {
        if (type.IsGenericParameter)
        {
            if (type.DeclaringMethod != null)
            {
                builder.Append("``").Append(type.GenericParameterPosition);
            }
            else
            {
                builder.Append('`').Append(type.GenericParameterPosition);
            }
            return;
        }

        if (type.HasElementType)
        {
            AppendParameterType(builder, type.GetElementType()!);
            if (type.IsArray)
            {
                // A multi-dimensional array lists a lower bound per dimension, as the compiler writes it:
                // int[,] is System.Int32[0:,0:]. A one-dimensional array is plain [].
                builder.Append('[');
                var rank = type.GetArrayRank();
                if (rank > 1)
                {
                    builder.Append("0:");
                    for (var i = 1; i < rank; i++) builder.Append(",0:");
                }
                builder.Append(']');
            }
            else if (type.IsByRef)
            {
                builder.Append('@');
            }
            else if (type.IsPointer)
            {
                builder.Append('*');
            }
            return;
        }

        if (type.DeclaringType != null)
        {
            AppendFullTypeName(builder, type.DeclaringType);
            builder.Append('.');
        }
        else if (!string.IsNullOrEmpty(type.Namespace))
        {
            builder.Append(type.Namespace).Append('.');
        }

        var name = type.Name;
        var index = name.IndexOf('`');
        if (index != -1) name = name.Substring(0, index);
        builder.Append(name);

        if (type.IsGenericType)
        {
            builder.Append('{');
            var genericArguments = type.GetGenericArguments();
            for (var i = 0; i < genericArguments.Length; i++)
            {
                if (i > 0) builder.Append(',');
                AppendParameterType(builder, genericArguments[i]);
            }
            builder.Append('}');
        }
    }
}
