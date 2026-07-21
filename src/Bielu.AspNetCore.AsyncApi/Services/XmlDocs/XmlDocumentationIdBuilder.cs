using System.Reflection;
using System.Text;

namespace Bielu.AspNetCore.AsyncApi.Services.XmlDocs;

internal static class XmlDocumentationIdBuilder
{
    public static string? CreateIdForMember(MemberInfo memberInfo)
    {
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
        if (type.IsGenericParameter)
        {
            builder.Append(type.GenericParameterPosition);
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
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var index = name.IndexOf('`');
            if (index != -1) name = name.Substring(0, index);
        }
        builder.Append(name);

        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments();
            if (type.IsGenericTypeDefinition)
            {
                builder.Append('`').Append(genericArguments.Length);
            }
            else
            {
                builder.Append('{');
                for (var i = 0; i < genericArguments.Length; i++)
                {
                    if (i > 0) builder.Append(',');
                    AppendParameterType(builder, genericArguments[i]);
                }
                builder.Append('}');
            }
        }
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
                builder.Append('[');
                var rank = type.GetArrayRank();
                if (rank > 1)
                {
                    for (var i = 0; i < rank - 1; i++) builder.Append(',');
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
