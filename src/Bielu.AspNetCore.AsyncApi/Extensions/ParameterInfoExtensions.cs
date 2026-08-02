// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

// Internal rather than public: this is a reflection helper for the generation path with a single
// call site, and as a public extension on ParameterInfo it would appear in IntelliSense on every
// ParameterInfo in any file importing this namespace. Narrowed before the 1.0.0 public API baseline
// froze it, since widening later is additive but narrowing would be a break.
internal static class ParameterInfoExtensions
{
    /// <summary>
    /// Determines whether the parameter's type declares the
    /// <c>public static ValueTask&lt;T&gt; BindAsync(HttpContext, ParameterInfo)</c> pattern that minimal
    /// APIs use for custom parameter binding.
    /// </summary>
    /// <remarks>
    /// Looks the method up reflectively, so the type's public static methods must survive trimming for
    /// the result to be meaningful. Only ever called from the reflection-based generation path, which
    /// is already annotated as such.
    /// </remarks>
    [RequiresUnreferencedCode(
        "Looks up BindAsync reflectively; the parameter type's public static methods may be trimmed.")]
    public static bool HasBindAsyncMethod(this ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var type = parameter.ParameterType;
        var method = type.GetMethod("BindAsync", BindingFlags.Public | BindingFlags.Static);
        if (method is null)
        {
            return false;
        }

        // Checked structurally rather than against typeof(ValueTask<>).MakeGenericType(type): the
        // latter needs runtime code generation, which native AOT cannot do. The old first comparison
        // against the *open* generic was also dead — a return type is never an open generic.
        var returnType = method.ReturnType;
        if (!returnType.IsGenericType ||
            returnType.GetGenericTypeDefinition() != typeof(ValueTask<>) ||
            returnType.GetGenericArguments()[0] != type)
        {
            return false;
        }

        var parameters = method.GetParameters();
        return parameters.Length == 2 &&
               parameters[0].ParameterType == typeof(HttpContext) &&
               parameters[1].ParameterType == typeof(ParameterInfo);
    }
}
