using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Covers payload shapes that take dedicated paths through schema generation (self-referencing types,
/// polymorphic hierarchies, dictionaries) and the type-activated document transformer registered via
/// <see cref="AsyncApiOptions.AddDocumentTransformer{TTransformerType}()"/>.
/// </summary>
public class SchemaShapeTransformerTests
{
    private const string TestDocumentName = "v1";

    private static async Task<(JsonElement Schemas, JsonElement Root, ConcurrentBag<Type> VisitedTypes)> GetDocumentAsync()
    {
        var visitedTypes = new ConcurrentBag<Type>();
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(new DescriptionStamp("Stamped by a DI-activated transformer."));
                    services.AddAsyncApi(TestDocumentName, options =>
                    {
                        options.AddDocumentTransformer<StampingDocumentTransformer>();
                        options.AddDocumentTransformer<SyncDisposableDocumentTransformer>();
                        options.AddSchemaTransformer((schema, context, _) =>
                        {
                            visitedTypes.Add(context.JsonTypeInfo.Type);
                            return Task.CompletedTask;
                        });
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAsyncApi());
                });
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();
        var response = await client.GetAsync($"/asyncapi/{TestDocumentName}.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        return (root.GetProperty("components").GetProperty("schemas"), root, visitedTypes);
    }

    [Fact]
    public async Task AddDocumentTransformerOfT_ActivatesWithDependenciesAndDisposes()
    {
        // Arrange
        var asyncDisposalsBefore = StampingDocumentTransformer.Disposals;
        var syncDisposalsBefore = SyncDisposableDocumentTransformer.Disposals;

        // Act
        var (_, root, _) = await GetDocumentAsync();

        // Assert
        root.GetProperty("info").GetProperty("description").GetString()
            .ShouldBe("Stamped by a DI-activated transformer.");
        StampingDocumentTransformer.Disposals.ShouldBeGreaterThan(asyncDisposalsBefore);
        SyncDisposableDocumentTransformer.Disposals.ShouldBeGreaterThan(syncDisposalsBefore);
    }

    [Fact]
    public async Task GetAsyncApiDocument_SelfReferencingPayload_ProducesSchema()
    {
        // Act
        var (schemas, _, _) = await GetDocumentAsync();

        // Assert
        schemas.TryGetProperty("shapeTreeNode", out var tree).ShouldBeTrue();
        var properties = tree.GetProperty("properties");
        properties.TryGetProperty("label", out _).ShouldBeTrue();
        properties.TryGetProperty("children", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsyncApiDocument_PolymorphicPayload_RunsSchemaTransformersOnDerivedTypes()
    {
        // Act
        var (schemas, _, visitedTypes) = await GetDocumentAsync();

        // Assert
        schemas.TryGetProperty("shapeVehicle", out _).ShouldBeTrue();
        visitedTypes.ShouldContain(typeof(ShapeVehicle));
        visitedTypes.ShouldContain(typeof(ShapeCar));
        visitedTypes.ShouldContain(typeof(ShapeTruck));
    }

    [Fact]
    public async Task GetAsyncApiDocument_DictionaryPayload_RunsSchemaTransformersOnValues()
    {
        // Act
        var (schemas, _, visitedTypes) = await GetDocumentAsync();

        // Assert
        schemas.TryGetProperty("shapeSensorReport", out var report).ShouldBeTrue();
        report.GetProperty("properties").TryGetProperty("readings", out _).ShouldBeTrue();
        visitedTypes.ShouldContain(typeof(ShapeMeasurement));
    }
}

public sealed record DescriptionStamp(string Value);

public sealed class StampingDocumentTransformer(DescriptionStamp stamp) : IAsyncApiDocumentTransformer, IAsyncDisposable
{
    private static int _disposals;

    public static int Disposals => Volatile.Read(ref _disposals);

    public Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Description = stamp.Value;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposals);
        return ValueTask.CompletedTask;
    }
}

public sealed class SyncDisposableDocumentTransformer : IAsyncApiDocumentTransformer, IDisposable
{
    private static int _disposals;

    public static int Disposals => Volatile.Read(ref _disposals);

    public Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public void Dispose() => Interlocked.Increment(ref _disposals);
}

[AsyncApi]
[Channel("shapes")]
public class ShapeBus
{
    [ReceiveOperation(typeof(ShapeTreeNode))]
    public void PublishTree(ShapeTreeNode tree) { }

    [ReceiveOperation(typeof(ShapeVehicle))]
    public void PublishVehicle(ShapeVehicle vehicle) { }

    [ReceiveOperation(typeof(ShapeSensorReport))]
    public void PublishReport(ShapeSensorReport report) { }
}

public class ShapeTreeNode
{
    public string Label { get; set; } = string.Empty;

    public List<ShapeTreeNode> Children { get; set; } = [];
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ShapeCar), "car")]
[JsonDerivedType(typeof(ShapeTruck), "truck")]
public abstract class ShapeVehicle
{
    public string Plate { get; set; } = string.Empty;
}

public sealed class ShapeCar : ShapeVehicle
{
    public int Seats { get; set; }
}

public sealed class ShapeTruck : ShapeVehicle
{
    public double PayloadTons { get; set; }
}

public class ShapeSensorReport
{
    public Dictionary<string, ShapeMeasurement> Readings { get; set; } = [];
}

public class ShapeMeasurement
{
    public double Value { get; set; }

    public string Unit { get; set; } = string.Empty;
}
