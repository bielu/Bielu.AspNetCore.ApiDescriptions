using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Models.Metadata;

namespace Bielu.AspNetCore.AsyncApi.Services;

[RequiresUnreferencedCode("ReflectionAsyncApiMetadataProvider performs reflection for discovery. This is not AOT compatible, use source generation instead.")]
[RequiresDynamicCode("ReflectionAsyncApiMetadataProvider performs reflection for discovery. This is not AOT compatible, use source generation instead.")]
internal sealed class ReflectionAsyncApiMetadataProvider : IAsyncApiMetadataProvider
{
    public IEnumerable<AsyncApiTypeMetadata> GetMetadata(string documentName)
    {
        foreach (var asm in GetCandidateAssembliesForAttributeScan())
        {
            foreach (var type in SafeGetTypes(asm))
            {
                if (type is null) continue;
                var asyncApiAttr = type.GetCustomAttribute<AsyncApiAttribute>(inherit: true);
                if (asyncApiAttr is null)
                    continue;

                if (asyncApiAttr.DocumentName is not null &&
                    !string.Equals(asyncApiAttr.DocumentName, documentName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var members = new List<MemberInfo> { type };
                members.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));

                var memberMetadatas = new List<AsyncApiMemberMetadata>();
                foreach (var member in members)
                {
                    var channelAttr = member.GetCustomAttribute<ChannelAttribute>(inherit: true);
                    if (channelAttr is null && member is MethodInfo)
                    {
                        channelAttr = type.GetCustomAttribute<ChannelAttribute>(inherit: true);
                    }

                    if (channelAttr is null)
                        continue;

                    memberMetadatas.Add(new AsyncApiMemberMetadata(
                        Member: member,
                        Channel: channelAttr,
                        Parameters: member.GetCustomAttributes<ChannelParameterAttribute>(inherit: true).ToList(),
                        Messages: member.GetCustomAttributes<MessageAttribute>(inherit: true).ToList(),
                        Operations: member.GetCustomAttributes<OperationAttribute>(inherit: true).ToList(),
                        MessageExamples: member.GetCustomAttributes<MessageExampleAttribute>(inherit: true).ToList()
                    ));
                }

                yield return new AsyncApiTypeMetadata(type, asyncApiAttr, memberMetadatas);
            }
        }
    }

    // Referenced assemblies already loaded by an earlier scan, or null when loading one failed.
    private readonly ConcurrentDictionary<string, Assembly?> _referencedAssemblies = new(StringComparer.Ordinal);

    /// <summary>
    /// The assemblies that may declare <see cref="AsyncApiAttribute"/> types, ordered by name so the document's
    /// channels and operations come out in the same order on every run.
    /// </summary>
    /// <remarks>
    /// The loaded assemblies alone are not enough: an assembly that holds only <c>[AsyncApi]</c> contracts is not
    /// loaded until code touches one of its types, which may be after the first document request, or never. So the
    /// references of every loaded application assembly are followed, transitively, and loaded. Framework assemblies
    /// are neither followed nor loaded, since they never reference the attributes.
    /// </remarks>
    private IEnumerable<Assembly> GetCandidateAssembliesForAttributeScan()
    {
        var attributesAssemblyName = typeof(AsyncApiAttribute).Assembly.GetName().Name;
        var ownAssembly = GetType().Assembly;
        var entry = Assembly.GetEntryAssembly();

        var assemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Append(entry))
        {
            Visit(assembly);
        }

        while (pending.TryDequeue(out var assembly))
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (!IsFrameworkAssembly(reference.Name))
                {
                    Visit(LoadReferencedAssembly(reference));
                }
            }
        }

        return assemblies.Values
            .Where(assembly => assembly != ownAssembly
                               && (assembly == entry
                                   || assembly.GetReferencedAssemblies().Any(reference => reference.Name == attributesAssemblyName)))
            .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .ToList();

        void Visit(Assembly? assembly)
        {
            if (assembly is { IsDynamic: false, FullName: { } name }
                && !IsFrameworkAssembly(assembly.GetName().Name)
                && assemblies.TryAdd(name, assembly))
            {
                pending.Enqueue(assembly);
            }
        }
    }

    private Assembly? LoadReferencedAssembly(AssemblyName reference) =>
        _referencedAssemblies.GetOrAdd(reference.FullName, static (_, name) =>
        {
            try
            {
                return Assembly.Load(name);
            }
            catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                // A reference that is not deployed with the app (an optional dependency, say) has no channels to give.
                return null;
            }
        }, reference);

    private static bool IsFrameworkAssembly(string? name) =>
        name is null or "mscorlib" or "netstandard" or "System"
        || name.StartsWith("System.", StringComparison.Ordinal)
        || name.StartsWith("Microsoft.", StringComparison.Ordinal);

    private static IEnumerable<Type?> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null) ?? Enumerable.Empty<Type>(); }
    }
}
