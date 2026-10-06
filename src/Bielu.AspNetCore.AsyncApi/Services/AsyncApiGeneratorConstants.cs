// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Services;

public static class AsyncApiGeneratorConstants
{
    public const string DefaultDocumentName = "v1";
    public const string DefaultAsyncApiVersion = "1.0.0";
    public const string DefaultAsyncApiRoute = "/asyncapi/{documentName}.json";
    internal const string DescriptionId = "x-aspnetcore-id";
    internal const string SchemaId = "x-schema-id";
    internal const string RefDefaultAnnotation = "x-ref-default";
    internal const string RefDescriptionAnnotation = "x-ref-description";
    internal const string RefExampleAnnotation = "x-ref-example";
    internal const string RefPrefix = "#";
    internal const string NullableProperty = "x-is-nullable-property";
    internal const string DefaultOpenApiResponseKey = "default";
    internal static readonly List<Type> PrimitiveTypes =
    [
        typeof(bool),
        typeof(byte),
        typeof(sbyte),
        typeof(byte[]),
        typeof(string),
        typeof(int),
        typeof(uint),
        typeof(nint),
        typeof(nuint),
        typeof(Int128),
        typeof(UInt128),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(Half),
        typeof(ulong),
        typeof(short),
        typeof(ushort),
        typeof(char),
        typeof(object),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeOnly),
        typeof(DateOnly),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(Uri),
        typeof(Version)
    ];
}
