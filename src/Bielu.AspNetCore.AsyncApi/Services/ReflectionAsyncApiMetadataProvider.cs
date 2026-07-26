using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
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

    private IEnumerable<Assembly> GetCandidateAssembliesForAttributeScan()
    {
        var targetAssemblyName = typeof(AsyncApiAttribute).Assembly.GetName();
        var partAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName != GetType().Assembly.FullName && !a.IsDynamic &&
            a.GetReferencedAssemblies().Any(x => x.Name == targetAssemblyName.Name));
        var entry = Assembly.GetEntryAssembly();
        return partAssemblies.Concat(entry is not null ? [entry] : []).Distinct();
    }

    private static IEnumerable<Type?> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null) ?? Enumerable.Empty<Type>(); }
    }
}
