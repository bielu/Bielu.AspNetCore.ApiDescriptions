// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Defaults used when generating AsyncAPI documents.
/// </summary>
public static class AsyncApiGeneratorConstants
{
    /// <summary>
    /// The name of the document registered by <c>AddAsyncApi()</c> when no name is given.
    /// </summary>
    public const string DefaultDocumentName = "v1";

    /// <summary>
    /// The <c>info.version</c> (the version of the described API, not of the AsyncAPI specification) of a
    /// document whose options don't set an <see cref="AsyncApiOptions.Info"/> version. The specification version is
    /// <see cref="AsyncApiOptions.AsyncApiVersion"/>.
    /// </summary>
    public const string DefaultInfoVersion = "1.0.0";

    /// <summary>
    /// The route pattern <c>MapAsyncApi()</c> serves documents on when none is configured.
    /// </summary>
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
