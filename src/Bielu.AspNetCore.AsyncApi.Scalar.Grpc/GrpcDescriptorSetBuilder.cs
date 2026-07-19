using System.Reflection;
using Google.Protobuf.Reflection;
using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Http;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc;

/// <summary>
/// Collects the protobuf <see cref="FileDescriptorSet" /> of the gRPC services mapped on an
/// application, by walking the endpoints' <see cref="GrpcMethodMetadata" /> back to the generated
/// service's <c>Descriptor</c>.
/// </summary>
internal static class GrpcDescriptorSetBuilder
{
    /// <summary>
    /// Builds a <see cref="FileDescriptorSet" /> covering every gRPC service among
    /// <paramref name="endpoints" />, with each file preceded by its imports (the topological order
    /// protoc emits, which descriptor-set consumers expect). Services without a discoverable
    /// descriptor (e.g. code-first services) are skipped.
    /// </summary>
    public static FileDescriptorSet Build(IEnumerable<Endpoint> endpoints)
    {
        var serviceTypes = endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<GrpcMethodMetadata>()?.ServiceType)
            .OfType<Type>()
            .Distinct();

        var set = new FileDescriptorSet();
        var seenFiles = new HashSet<string>();
        foreach (var serviceType in serviceTypes)
        {
            if (ResolveServiceDescriptor(serviceType) is { } descriptor)
            {
                AddFileWithDependencies(descriptor.File, set, seenFiles);
            }
        }

        return set;
    }

    /// <summary>
    /// Finds the generated <see cref="ServiceDescriptor" /> for a mapped service type. The
    /// implementation class derives from the generated <c>{Service}Base</c>, whose declaring type is
    /// the generated service container exposing a static <c>Descriptor</c> property.
    /// </summary>
    private static ServiceDescriptor? ResolveServiceDescriptor(Type serviceType)
    {
        for (Type? type = serviceType; type is not null; type = type.BaseType)
        {
            var container = type.DeclaringType;
            var property = container?.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static);
            if (property?.GetValue(null) is ServiceDescriptor descriptor)
            {
                return descriptor;
            }
        }

        return null;
    }

    private static void AddFileWithDependencies(FileDescriptor file, FileDescriptorSet set, HashSet<string> seenFiles)
    {
        if (!seenFiles.Add(file.Name))
        {
            return;
        }

        foreach (var dependency in file.Dependencies)
        {
            AddFileWithDependencies(dependency, set, seenFiles);
        }

        set.File.Add(file.ToProto());
    }
}
