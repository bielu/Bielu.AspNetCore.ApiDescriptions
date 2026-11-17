using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Transformers;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>Options for a single Arazzo document, configured via <c>AddArazzo</c>.</summary>
public sealed class ArazzoOptions
{
    /// <summary>The name of this document, used in the <c>MapArazzo</c> route and for keyed-service lookup.</summary>
    public string DocumentName { get; internal set; } = ArazzoDefaults.DefaultDocumentName;

    /// <summary>
    /// The Arazzo Specification version the document declares in its <c>arazzo</c> field. Defaults to
    /// <see cref="ArazzoVersion.V1_1"/>.
    /// </summary>
    public ArazzoVersion Version { get; set; } = ArazzoVersion.V1_1;

    /// <summary>
    /// The name this document is listed under when it is generated at build time (<c>dotnet getdocument</c>).
    /// By default it is <see cref="DocumentName"/>, or <c>arazzo-</c> followed by it when an AsyncAPI or OpenAPI
    /// document already has that name. It changes only the build-time name; the <c>MapArazzo</c> route is
    /// not affected.
    /// </summary>
    public string? BuildTimeDocumentName { get; set; }

    /// <summary>The document's <c>info</c> object. Set via <see cref="WithInfo(string,string)"/>.</summary>
    public ArazzoInfo? Info { get; set; }

    /// <summary>
    /// When true (the default), every workflow step's <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c>
    /// is resolved against the live self-wired AsyncAPI/OpenAPI documents at app startup, so a renamed
    /// channel or operation fails startup instead of failing in production.
    /// </summary>
    public bool ValidateSourceReferencesOnStartup { get; set; } = true;

    private TimeSpan _startupValidationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The maximum time startup validation waits for this document's self-wired AsyncAPI/OpenAPI documents
    /// to be generated before failing startup with a timeout error instead of hanging indefinitely (a
    /// misbehaving document transformer can otherwise block <c>host.StartAsync()</c> forever). Defaults to
    /// 30 seconds; only applies when <see cref="ValidateSourceReferencesOnStartup"/> is true.
    /// </summary>
    public TimeSpan StartupValidationTimeout
    {
        get => _startupValidationTimeout;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "StartupValidationTimeout must be positive.");
            }

            _startupValidationTimeout = value;
        }
    }

    internal List<ArazzoSourceDescription> SourceDescriptions { get; } = [];

    internal List<ArazzoWorkflow> Workflows { get; } = [];

    internal List<SourceWiring> SourceWirings { get; } = [];

    internal string? Self { get; private set; }

    internal ArazzoComponents? Components { get; private set; }

    internal Dictionary<string, JsonNode?>? Extensions { get; private set; }

    /// <summary>
    /// Sets the document's <c>$self</c> (Arazzo 1.1): the URI the document identifies itself by, which is also
    /// the base URI relative references in it resolve against.
    /// </summary>
    /// <param name="uri">An absolute or relative URI reference with no fragment.</param>
    public ArazzoOptions WithSelf(string uri)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        if (!Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out _) || uri.Contains('#', StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{uri}' is not a URI reference without a fragment, as $self requires.", nameof(uri));
        }

        Self = uri;
        return this;
    }

    /// <summary>
    /// Adds a Specification Extension (<c>x-*</c>) to the document root. Setting the same name again replaces
    /// its value.
    /// </summary>
    /// <param name="name">The extension name, which must start with <c>x-</c>.</param>
    /// <param name="value">The extension value; any JSON, including <c>null</c>.</param>
    public ArazzoOptions WithExtension(string name, JsonNode? value)
    {
        ArazzoExtensions.Set(Extensions ??= new Dictionary<string, JsonNode?>(StringComparer.Ordinal), name, value);
        return this;
    }

    /// <summary>
    /// Adds a reusable JSON Schema to <c>components.inputs</c>, which workflows refer to with
    /// <see cref="ArazzoWorkflowBuilder.WithInputsReference"/>.
    /// </summary>
    /// <param name="name">The component name; one matching <c>^[a-zA-Z0-9\.\-_]+$</c>, unique among the inputs.</param>
    /// <param name="schema">The JSON Schema.</param>
    public ArazzoOptions AddComponentInputs(string name, JsonNode schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        AddComponent(GetComponents().Inputs ??= new Dictionary<string, JsonNode?>(StringComparer.Ordinal), name, schema, "inputs");
        return this;
    }

    /// <summary>
    /// Adds a reusable JSON Schema to <c>components.inputs</c> from a source-generated <see cref="JsonTypeInfo"/>,
    /// avoiding reflection so the overload stays trim/AOT-safe.
    /// </summary>
    /// <param name="name">The component name, unique among the inputs.</param>
    /// <param name="typeInfo">The type whose JSON Schema is added.</param>
    public ArazzoOptions AddComponentInputs(string name, JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return AddComponentInputs(name, JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo));
    }

    /// <summary>
    /// Adds a reusable Parameter Object to <c>components.parameters</c>, which workflows, steps and failure
    /// actions refer to with <c>ParameterReference</c>.
    /// </summary>
    /// <param name="name">The component name, unique among the parameters.</param>
    /// <param name="parameter">The parameter.</param>
    /// <example>
    /// <code>
    /// options.AddComponentParameter("page", new ArazzoParameter
    /// {
    ///     Name = "page",
    ///     In = ArazzoParameterLocation.Query,
    ///     Value = ArazzoValue.FromLiteral(1),
    /// });
    /// options.AddWorkflow("listOrders", wf => wf.Step("list", s => s
    ///     .Operation("listOrders")
    ///     .ParameterReference("page", "2")));
    /// </code>
    /// </example>
    public ArazzoOptions AddComponentParameter(string name, ArazzoParameter parameter)
    {
        ArazzoParameterGuard.Validate(parameter);
        AddComponent(GetComponents().Parameters ??= new Dictionary<string, ArazzoParameter>(StringComparer.Ordinal), name, parameter, "parameters");
        return this;
    }

    /// <summary>
    /// Adds a reusable Success Action Object to <c>components.successActions</c>, which workflows and steps
    /// refer to with <c>OnSuccessReference</c>.
    /// </summary>
    /// <param name="name">The component name, unique among the success actions.</param>
    /// <param name="action">The success action.</param>
    public ArazzoOptions AddComponentSuccessAction(string name, ArazzoSuccessAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        AddComponent(GetComponents().SuccessActions ??= new Dictionary<string, ArazzoSuccessAction>(StringComparer.Ordinal), name, action, "successActions");
        return this;
    }

    /// <summary>
    /// Adds a reusable Failure Action Object to <c>components.failureActions</c>, which workflows and steps
    /// refer to with <c>OnFailureReference</c>.
    /// </summary>
    /// <param name="name">The component name, unique among the failure actions.</param>
    /// <param name="action">The failure action.</param>
    public ArazzoOptions AddComponentFailureAction(string name, ArazzoFailureAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        AddComponent(GetComponents().FailureActions ??= new Dictionary<string, ArazzoFailureAction>(StringComparer.Ordinal), name, action, "failureActions");
        return this;
    }

    private ArazzoComponents GetComponents() => Components ??= new ArazzoComponents();

    private static void AddComponent<T>(IDictionary<string, T> components, string name, T value, string kind)
    {
        ArazzoIdentifier.ValidateKey(name, nameof(name));
        if (!components.TryAdd(name, value))
        {
            throw new ArgumentException($"A component named '{name}' is already in components.{kind}.", nameof(name));
        }
    }

    /// <summary>Sets the document's title and version.</summary>
    public ArazzoOptions WithInfo(string title, string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(version);
        Info = new ArazzoInfo { Title = title, Version = version };
        return this;
    }

    /// <summary>Configures the document's <c>info</c> object.</summary>
    public ArazzoOptions WithInfo(Action<ArazzoInfo> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Info ??= new ArazzoInfo { Title = DocumentName, Version = "1.0.0" };
        configure(Info);
        return this;
    }

    /// <summary>Adds a source description pointing at an arbitrary URL. Prefer <see cref="AddAsyncApiSource"/>/<see cref="AddOpenApiSource"/> to self-wire against a document served by this same app.</summary>
    public ArazzoOptions AddSourceDescription(string name, string url, string type)
    {
        ArazzoIdentifier.Validate(name, nameof(name));
        ArgumentException.ThrowIfNullOrEmpty(url);
        ArgumentException.ThrowIfNullOrEmpty(type);
        SourceDescriptions.Add(new ArazzoSourceDescription { Name = name, Url = url, Type = type });
        return this;
    }

    /// <summary>
    /// Adds a source description of type <c>asyncapi</c> that self-wires against the AsyncAPI document named
    /// <paramref name="asyncApiDocumentName"/> served by this same app (via <c>Bielu.AspNetCore.AsyncApi</c>'s
    /// <c>AddAsyncApi</c>/<c>MapAsyncApi</c>). When <see cref="ValidateSourceReferencesOnStartup"/> is true,
    /// this document's live, in-memory contents are used to validate every step referencing this source.
    /// </summary>
    /// <param name="sourceName">The name this source is referred to as within workflow steps (<c>sourceDescriptions.NAME</c>).</param>
    /// <param name="asyncApiDocumentName">The document name passed to <c>AddAsyncApi</c>/<c>MapAsyncApi</c> for the target document.</param>
    /// <param name="url">The externally reachable URL the source description should advertise. Defaults to <c>/asyncapi/{asyncApiDocumentName}.json</c>.</param>
    public ArazzoOptions AddAsyncApiSource(string sourceName, string asyncApiDocumentName, string? url = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(asyncApiDocumentName);
        var lowercasedDocumentName = asyncApiDocumentName.ToLowerInvariant();
        AddSourceDescription(sourceName, url ?? $"/asyncapi/{lowercasedDocumentName}.json",
            ArazzoSourceDescriptionType.AsyncApi);
        SourceWirings.Add(new SourceWiring(sourceName, ArazzoSourceDescriptionType.AsyncApi, lowercasedDocumentName));
        return this;
    }

    /// <summary>
    /// Adds a source description of type <c>openapi</c> that self-wires against the OpenAPI document named
    /// <paramref name="openApiDocumentName"/> served by this same app (via <c>Microsoft.AspNetCore.OpenApi</c>'s
    /// <c>AddOpenApi</c>/<c>MapOpenApi</c>). When <see cref="ValidateSourceReferencesOnStartup"/> is true,
    /// this document's live, in-memory contents are used to validate every step referencing this source.
    /// </summary>
    /// <param name="sourceName">The name this source is referred to as within workflow steps (<c>sourceDescriptions.NAME</c>).</param>
    /// <param name="openApiDocumentName">The document name passed to <c>AddOpenApi</c>/<c>MapOpenApi</c> for the target document.</param>
    /// <param name="url">The externally reachable URL the source description should advertise. Defaults to <c>/openapi/{openApiDocumentName}.json</c>.</param>
    public ArazzoOptions AddOpenApiSource(string sourceName, string openApiDocumentName, string? url = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(openApiDocumentName);
        AddSourceDescription(sourceName, url ?? $"/openapi/{openApiDocumentName}.json",
            ArazzoSourceDescriptionType.OpenApi);
        SourceWirings.Add(new SourceWiring(sourceName, ArazzoSourceDescriptionType.OpenApi, openApiDocumentName));
        return this;
    }

    /// <summary>Adds a workflow to the document.</summary>
    public ArazzoOptions AddWorkflow(string workflowId, Action<ArazzoWorkflowBuilder> configure)
    {
        ArazzoIdentifier.Validate(workflowId, nameof(workflowId));
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ArazzoWorkflowBuilder(workflowId);
        configure(builder);
        Workflows.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a workflow identified by the marker type <typeparamref name="TWorkflow"/>, whose
    /// <c>workflowId</c> follows the <see cref="ArazzoId"/> convention. Lets other workflows and steps
    /// reference it as <c>DependsOn&lt;TWorkflow&gt;()</c>/<c>Workflow&lt;TWorkflow&gt;()</c> instead of
    /// repeating a string id.
    /// </summary>
    /// <typeparam name="TWorkflow">The marker type naming this workflow.</typeparam>
    public ArazzoOptions AddWorkflow<TWorkflow>(Action<ArazzoWorkflowBuilder> configure)
        => AddWorkflow(ArazzoId.FromType<TWorkflow>(), configure);

    internal List<IArazzoSerializedDocumentTransformer> SerializedDocumentTransformers { get; } = [];

    /// <summary>
    /// Registers a transformer that rewrites this document after it has been serialized, in registration
    /// order, each against the output of the last.
    /// </summary>
    /// <param name="transformer">The transformer to register.</param>
    public ArazzoOptions AddSerializedDocumentTransformer(IArazzoSerializedDocumentTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        SerializedDocumentTransformers.Add(transformer);
        return this;
    }

    /// <summary>Registers a delegate as a serialized-document transformer on this document.</summary>
    /// <param name="transformer">The delegate representing the transformer.</param>
    public ArazzoOptions AddSerializedDocumentTransformer(
        Func<string, ArazzoSerializedDocumentContext, CancellationToken, ValueTask<string>> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        SerializedDocumentTransformers.Add(new DelegateArazzoSerializedDocumentTransformer(transformer));
        return this;
    }

    private sealed class DelegateArazzoSerializedDocumentTransformer(
        Func<string, ArazzoSerializedDocumentContext, CancellationToken, ValueTask<string>> transformer)
        : IArazzoSerializedDocumentTransformer
    {
        public ValueTask<string> TransformAsync(string document, ArazzoSerializedDocumentContext context,
            CancellationToken cancellationToken)
            => transformer(document, context, cancellationToken);
    }

    /// <summary>Associates a self-wired source description with the document name it resolves against at runtime.</summary>
    internal readonly record struct SourceWiring(string SourceName, string SourceType, string DocumentName);
}
