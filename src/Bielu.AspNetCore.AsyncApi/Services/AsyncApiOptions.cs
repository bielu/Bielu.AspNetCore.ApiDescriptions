// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Transformers;
using Bielu.AspNetCore.AsyncApi.Helpers;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Options to support the construction of AsyncApi documents.
/// </summary>
public sealed class AsyncApiOptions
{
    internal readonly List<IAsyncApiDocumentTransformer> DocumentTransformers = [];
    internal readonly List<IAsyncApiOperationTransformer> OperationTransformers = [];
    internal readonly List<IAsyncApiSchemaTransformer> SchemaTransformers = [];
    internal Dictionary<string, AsyncApiServer> Servers { get; set; } = new();

    /// <summary>
    /// A default implementation for creating a schema reference ID for a given <see cref="JsonTypeInfo"/>.
    /// </summary>
    /// <param name="jsonTypeInfo">The <see cref="JsonTypeInfo"/> associated with the schema we are generating a reference ID for.</param>
    /// <returns>The reference ID to use for the schema or <see langword="null"/> if the schema should always be inlined.</returns>
    public static string? CreateDefaultSchemaReferenceId(JsonTypeInfo jsonTypeInfo) => jsonTypeInfo.GetSchemaReferenceId();

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncApiOptions"/> class
    /// with the default <see cref="ShouldInclude"/> predicate.
    /// </summary>
    public AsyncApiOptions()
    {
        ShouldInclude = (description) => description.GroupName == null || string.Equals(description.GroupName, DocumentName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The version of the AsyncApi specification to use. Defaults to <see cref="AsyncApiSpecVersion.AsyncApi3_1"/>.
    /// </summary>
    public AsyncApiVersion AsyncApiVersion { get; set; } = AsyncApiVersion.AsyncApi3_0;

    /// <summary>
    /// The name of the AsyncApi document this <see cref="AsyncApiOptions"/> instance is associated with.
    /// </summary>
    public string DocumentName { get; internal set; } = AsyncApiGeneratorConstants.DefaultDocumentName;

    /// <summary>
    /// A delegate to determine whether a given <see cref="ApiDescription"/> should be included in the given AsyncApi document.
    /// </summary>
    public Func<ApiDescription, bool> ShouldInclude { get; set; }

    /// <summary>
    /// A delegate to determine how reference IDs should be created for schemas associated with types in the given AsyncApi document.
    /// </summary>
    /// <remarks>
    /// The default implementation uses the <see cref="CreateDefaultSchemaReferenceId"/> method to generate reference IDs. When
    /// the provided delegate returns <see langword="null"/>, the schema associated with the <see cref="JsonTypeInfo"/> will always be inlined.
    /// </remarks>
    public Func<JsonTypeInfo, string?> CreateSchemaReferenceId { get; set; } = CreateDefaultSchemaReferenceId;

    /// <summary>
    /// Registers a new document transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <typeparam name="TTransformerType">The type of the <see cref="IAsyncApiDocumentTransformer"/> to instantiate.</typeparam>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddDocumentTransformer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransformerType>()
        where TTransformerType : IAsyncApiDocumentTransformer
    {
        DocumentTransformers.Add(new TypeBasedAsyncApiDocumentTransformer(typeof(TTransformerType)));
        return this;
    }

    /// <summary>
    /// Registers a given instance of <see cref="IAsyncApiDocumentTransformer"/> on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The <see cref="IAsyncApiDocumentTransformer"/> instance to use.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddDocumentTransformer(IAsyncApiDocumentTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        DocumentTransformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Registers a given delegate as a document transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The delegate representing the document transformer.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddDocumentTransformer(Func<AsyncApiDocument, AsyncApiDocumentTransformerContext, CancellationToken, Task> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        DocumentTransformers.Add(new DelegateAsyncApiDocumentTransformer(transformer));
        return this;
    }

    /// <summary>
    /// Registers a new operation transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <typeparam name="TTransformerType">The type of the <see cref="IAsyncApiOperationTransformer"/> to instantiate.</typeparam>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddOperationTransformer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransformerType>()
        where TTransformerType : IAsyncApiOperationTransformer
    {
        OperationTransformers.Add(new TypeBasedAsyncApiOperationTransformer(typeof(TTransformerType)));
        return this;
    }

    /// <summary>
    /// Registers a given instance of <see cref="IAsyncApiOperationTransformer"/> on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The <see cref="IAsyncApiOperationTransformer"/> instance to use.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddOperationTransformer(IAsyncApiOperationTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        OperationTransformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Registers a given delegate as an operation transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The delegate representing the operation transformer.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddOperationTransformer(Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        OperationTransformers.Add(new DelegateAsyncApiOperationTransformer(transformer));
        return this;
    }

    /// <summary>
    /// Registers a new schema transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <typeparam name="TTransformerType">The type of the <see cref="IAsyncApiSchemaTransformer"/> to instantiate.</typeparam>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddSchemaTransformer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransformerType>()
        where TTransformerType : IAsyncApiSchemaTransformer
    {
        SchemaTransformers.Add(new TypeBasedAsyncApiSchemaTransformer(typeof(TTransformerType)));
        return this;
    }

    /// <summary>
    /// Registers a given instance of <see cref="IAsyncApiOperationTransformer"/> on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The <see cref="IAsyncApiOperationTransformer"/> instance to use.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddSchemaTransformer(IAsyncApiSchemaTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        SchemaTransformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Registers a given delegate as a schema transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The delegate representing the schema transformer.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddSchemaTransformer(Func<AsyncApiJsonSchema, AsyncApiJsonSchemaTransformerContext, CancellationToken, Task> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        SchemaTransformers.Add(new DelegateAsyncApiSchemaTransformer(transformer));
        return this;
    }

    /// <summary>
    /// Adds a server to the AsyncApi document.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <param name="url">The server URL.</param>
    /// <param name="protocol">The server protocol (mqtt, http, ws, etc.).</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddServer(string name, string url, string protocol, string pathName = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(protocol);

        // Extract host from URL (remove protocol prefix if present)
        var host = url.Contains("://") ? url.Split("://")[1] : url;
        var sanitizedName = AsyncApiNamingHelper.SanitizeKey(name);

        Servers[sanitizedName] = new AsyncApiServer { Host = host, PathName = pathName, Protocol = protocol };
        return this;
    }
    public AsyncApiOptions AddServer(string name, string url, string protocol, Action<AsyncApiServer> configure)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(configure);

        var host = url.Contains("://") ? url.Split("://")[1] : url;
        var server = new AsyncApiServer 
        { 
            Host = host, 
            PathName = null, 
            Protocol = protocol 
        };
        var sanitizedName = AsyncApiNamingHelper.SanitizeKey(name);
        
        configure(server);
        Servers[sanitizedName] = server;
        return this;
    }
    /// <summary>
    /// The default content type for messages in the AsyncApi document.
    /// </summary>
    public string? DefaultContentType { get; set; }

    /// <summary>
    /// The info object for the AsyncApi document.
    /// </summary>
    public AsyncApiInfo? Info { get; set; }

    /// <summary>
    /// Sets the default content type for the AsyncApi document.
    /// </summary>
    public AsyncApiOptions WithDefaultContentType(string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        DefaultContentType = contentType;
        return this;
    }

    /// <summary>
    /// Sets the info properties for the AsyncApi document.
    /// </summary>
    public AsyncApiOptions WithInfo(string title, string version)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(version);
        Info ??= new AsyncApiInfo();
        Info.Title = title;
        Info.Version = version;
        return this;
    }

    /// <summary>
    /// Configures the info object using a delegate.
    /// </summary>
    public AsyncApiOptions WithInfo(Action<AsyncApiInfo> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Info ??= new AsyncApiInfo();
        configure(Info);
        return this;
    }

    /// <summary>
    /// Sets the description for the AsyncApi document.
    /// </summary>
    public AsyncApiOptions WithDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        Info ??= new AsyncApiInfo();
        Info.Description = description;
        return this;
    }

    /// <summary>
    /// Sets the license for the AsyncApi document.
    /// </summary>
    public AsyncApiOptions WithLicense(string name, string? url = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Info ??= new AsyncApiInfo();
        Info.License = new AsyncApiLicense { Name = name   };
        if(url != null)
        {
            Info.License.Url = new  Uri(url);
        }
        return this;
    }
    /// <summary>
    /// Operation bindings collection.
    /// </summary>
    public Dictionary<string, IList<IOperationBinding>> OperationBindings { get; set; } = new();

    /// <summary>
    /// Channel bindings collection.
    /// </summary>
    public Dictionary<string, IList<IChannelBinding>> ChannelBindings { get; set; } = new();

    public string DocumentRoutePattern { get; set; }

    /// <summary>
    /// When non-empty, only channels whose sanitized key appears in this set are included in the
    /// generated document. Useful when multiple hubs share an assembly and you need each AsyncAPI
    /// document to expose only its own hub. Comparison is case-insensitive.
    /// </summary>
    public HashSet<string> IncludeOnlyChannels { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a channel name to <see cref="IncludeOnlyChannels"/>, sanitizing it the same way channel
    /// keys are sanitized during document generation, so raw channel names (e.g. containing slashes
    /// or spaces) match the generated keys.
    /// </summary>
    public AsyncApiOptions AddIncludedChannel(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        IncludeOnlyChannels.Add(AsyncApiNamingHelper.SanitizeKey(name));
        return this;
    }

    /// <summary>
    /// Adds an operation binding.
    /// </summary>
    public AsyncApiOptions AddOperationBinding(string name, IOperationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(binding);

        if (!OperationBindings.ContainsKey(name))
        {
            OperationBindings[name] = new List<IOperationBinding>();
        }

        OperationBindings[name].Add(binding);
        return this;
    }

    /// <summary>
    /// Adds a channel binding.
    /// </summary>
    public AsyncApiOptions AddChannelBinding(string name, IChannelBinding binding)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(binding);

        if (!ChannelBindings.ContainsKey(name))
        {
            ChannelBindings[name] = new List<IChannelBinding>();
        }

        ChannelBindings[name].Add(binding);
        return this;
    }
}
