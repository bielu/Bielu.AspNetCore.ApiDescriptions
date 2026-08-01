// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
    internal readonly List<IAsyncApiSerializedDocumentTransformer> SerializedDocumentTransformers = [];
    internal Dictionary<string, AsyncApiServer> Servers { get; set; } = new();

    /// <summary>
    /// A default implementation for creating a schema reference ID for a given <see cref="JsonTypeInfo"/>.
    /// </summary>
    /// <param name="jsonTypeInfo">The <see cref="JsonTypeInfo"/> associated with the schema we are generating a reference ID for.</param>
    /// <returns>The reference ID to use for the schema or <see langword="null"/> if the schema should always be inlined.</returns>
    public static string? CreateDefaultSchemaReferenceId(JsonTypeInfo jsonTypeInfo) =>
        jsonTypeInfo.GetSchemaReferenceId();

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncApiOptions"/> class
    /// with the default <see cref="ShouldInclude"/> predicate.
    /// </summary>
    public AsyncApiOptions()
    {
        ShouldInclude = (description) => description.GroupName == null ||
                                         string.Equals(description.GroupName, DocumentName,
                                             StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The version of the AsyncAPI specification to use. Defaults to <see cref="AsyncApiVersion.AsyncApi3_0"/>,
    /// which emits documents declaring <c>3.1.0</c>; <see cref="AsyncApiVersion.AsyncApi2_0"/> emits <c>2.6.0</c>.
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
    public AsyncApiOptions AddDocumentTransformer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TTransformerType>()
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
    public AsyncApiOptions AddDocumentTransformer(
        Func<AsyncApiDocument, AsyncApiDocumentTransformerContext, CancellationToken, Task> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        DocumentTransformers.Add(new DelegateAsyncApiDocumentTransformer(transformer));
        return this;
    }

    /// <summary>
    /// Automatically populates <c>components.securitySchemes</c> from the ASP.NET Core authentication
    /// schemes registered on the application (resolved at document-generation time from
    /// <see cref="Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider"/>), and — unless
    /// disabled — references them from the document's servers so consumers treat them as required.
    /// </summary>
    /// <remarks>
    /// Built-in handlers (JWT bearer, cookies, Negotiate) are mapped out of the box. Handlers whose
    /// shape cannot be inferred (custom API-key handlers, OAuth2/OpenID Connect flows) are skipped by
    /// the default mapper; describe those by setting <see cref="AuthenticationDetectionOptions.Map"/>.
    /// Detection never overwrites a hand-authored scheme of the same name unless
    /// <see cref="AuthenticationDetectionOptions.OverwriteExisting"/> is set, so this composes with
    /// explicitly declared schemes. Call this only on documents that should surface authentication —
    /// it is opt-in per document.
    /// </remarks>
    /// <param name="configure">An optional delegate to customize the detection behavior.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions DetectAuthenticationSchemes(Action<AuthenticationDetectionOptions>? configure = null)
    {
        var detectionOptions = new AuthenticationDetectionOptions();
        configure?.Invoke(detectionOptions);
        DocumentTransformers.Add(new AuthenticationSchemeDocumentTransformer(detectionOptions));
        return this;
    }

    /// <summary>
    /// Registers a new operation transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <typeparam name="TTransformerType">The type of the <see cref="IAsyncApiOperationTransformer"/> to instantiate.</typeparam>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddOperationTransformer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TTransformerType>()
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
    public AsyncApiOptions AddOperationTransformer(
        Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task> transformer)
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
    public AsyncApiOptions AddSchemaTransformer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TTransformerType>()
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
    public AsyncApiOptions AddSchemaTransformer(
        Func<AsyncApiJsonSchema, AsyncApiJsonSchemaTransformerContext, CancellationToken, Task> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        SchemaTransformers.Add(new DelegateAsyncApiSchemaTransformer(transformer));
        return this;
    }

    /// <summary>
    /// Registers a given instance of <see cref="IAsyncApiSerializedDocumentTransformer"/> on the current
    /// <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The <see cref="IAsyncApiSerializedDocumentTransformer"/> instance to use.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    /// <remarks>
    /// Serialized-document transformers run after the document has been written out, in registration
    /// order, each against the output of the last. Prefer <see cref="AddDocumentTransformer(IAsyncApiDocumentTransformer)"/>
    /// unless the transformation is genuinely one over the wire representation.
    /// </remarks>
    public AsyncApiOptions AddSerializedDocumentTransformer(IAsyncApiSerializedDocumentTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        SerializedDocumentTransformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Registers a given delegate as a serialized-document transformer on the current
    /// <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The delegate representing the serialized-document transformer.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddSerializedDocumentTransformer(
        Func<string, AsyncApiSerializedDocumentContext, CancellationToken, ValueTask<string>> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        SerializedDocumentTransformers.Add(new DelegateAsyncApiSerializedDocumentTransformer(transformer));
        return this;
    }

    /// <summary>
    /// Adds a server to the AsyncApi document.
    /// </summary>
    /// <param name="name">The server name.</param>
    /// <param name="url">The server URL.</param>
    /// <param name="protocol">The server protocol (mqtt, http, ws, etc.).</param>
    /// <param name="pathName">An optional path appended to the server URL. Omit for a server addressed at its root.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddServer(string name, string url, string protocol, string? pathName = null)
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
        var server = new AsyncApiServer { Host = host, PathName = null, Protocol = protocol };
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
        Info.License = new AsyncApiLicense { Name = name };
        if (url != null)
        {
            Info.License.Url = new Uri(url);
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

    /// <summary>
    /// Gets the list of XML documentation files to use for populating descriptions.
    /// </summary>
    internal List<string> XmlDocumentationFiles { get; } = [];

    /// <summary>
    /// Includes the XML documentation from the specified file path to populate descriptions.
    /// </summary>
    /// <param name="filePath">The path to the XML documentation file.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions IncludeXmlComments(string filePath)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            XmlDocumentationFiles.Add(filePath);
        }

        return this;
    }

    /// <summary>
    /// Includes the XML documentation for the specified assembly to populate descriptions.
    /// </summary>
    /// <param name="assembly">The assembly to include XML documentation for.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    [UnconditionalSuppressMessage("SingleFile", "IL3000",
        Justification = "An empty Assembly.Location is exactly the single-file case this method " +
                        "handles: it falls back to AppContext.BaseDirectory rather than using the value.")]
    public AsyncApiOptions IncludeXmlComments(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        // Assembly.Location is an empty string for an assembly embedded in a single-file app, which
        // would otherwise reduce the path to a bare "{Name}.xml" resolved against the current working
        // directory. AppContext.BaseDirectory is the app directory in both layouts.
        var assemblyDirectory = string.IsNullOrEmpty(assembly.Location)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(assembly.Location);

        var filePath = Path.Combine(
            assemblyDirectory is { Length: > 0 } directory ? directory : AppContext.BaseDirectory,
            $"{assembly.GetName().Name}.xml");

        return IncludeXmlComments(filePath);
    }

    /// <summary>
    /// Collection of message examples registered fluently.
    /// Key is the payload type, value is a list of named examples.
    /// </summary>
    internal Dictionary<Type, List<MessageExample>> MessageExamples { get; } = [];

    /// <summary>
    /// When set to <see langword="true"/>, the first message example found for a payload type
    /// will also be surfaced as the <c>example</c> property in its JSON schema.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool SetSchemaExampleFromMessageExample { get; set; }

    /// <summary>
    /// Adds an example for a specific message payload type.
    /// </summary>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <param name="name">A machine-friendly name for the example.</param>
    /// <param name="payload">The example payload instance.</param>
    /// <param name="summary">An optional short summary of the example.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddMessageExample<TPayload>(string name, TPayload payload, string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(payload);

        var type = typeof(TPayload);
        if (!MessageExamples.ContainsKey(type))
        {
            MessageExamples[type] = [];
        }

        MessageExamples[type].Add(new MessageExample { Name = name, Value = payload, Summary = summary });
        return this;
    }
}

internal class MessageExample
{
    public string Name { get; set; }
    public object Value { get; set; }
    public string? Summary { get; set; }
}
