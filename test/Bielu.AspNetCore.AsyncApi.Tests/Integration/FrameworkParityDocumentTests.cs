using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// The suite runs once per target framework (net10.0 and net11.0), and every run compares against the
/// same golden file. A pass on both therefore means both runtimes produce the same document. The
/// fixture covers the schema features System.Text.Json's exporter decides, which a runtime upgrade can
/// change quietly: nullability, <c>required</c>, enums, polymorphism, dictionaries, validation
/// attributes and shared <c>$ref</c>s.
/// </summary>
/// <remarks>
/// After an intended change, regenerate the files with <c>BIELU_UPDATE_GOLDEN=1 dotnet test -f net10.0</c>
/// and review the diff. Then run the net11.0 tests without the variable to confirm parity. Never
/// regenerate from net11.0 alone to make a difference go away.
/// </remarks>
public class FrameworkParityDocumentTests
{
    private const string DocumentName = "framework-parity";

    public enum ParityPriority
    {
        Low,
        Normal,
        High,
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(ParityCircle), "circle")]
    [JsonDerivedType(typeof(ParitySquare), "square")]
    public abstract class ParityShape
    {
        public string? Label { get; set; }
    }

    public sealed class ParityCircle : ParityShape
    {
        public double Radius { get; set; }
    }

    public sealed class ParitySquare : ParityShape
    {
        public double Side { get; set; }
    }

    public sealed record ParityAddress(string Street, string? City);

    /// <summary>A message exercising the schema features listed on the test class.</summary>
    public sealed class ParityMessage
    {
        /// <summary>Identifies the message.</summary>
        public required Guid Id { get; init; }

        [StringLength(64, MinimumLength = 1)]
        public required string Name { get; init; }

        public string? Nickname { get; set; }

        [Range(0, 100)]
        public int Score { get; set; }

        public long? OptionalCount { get; set; }

        public decimal Amount { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public TimeSpan? Duration { get; set; }

        public ParityPriority Priority { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter<ParityPriority>))]
        public ParityPriority NamedPriority { get; set; }

        public ParityPriority? MaybePriority { get; set; }

        public ParityShape? Shape { get; set; }

        public ParityAddress Home { get; set; } = new("Main", null);

        public ParityAddress? Work { get; set; }

        public List<ParityAddress> Previous { get; set; } = [];

        public string[] Tags { get; set; } = [];

        public Dictionary<string, int> Counters { get; set; } = [];

        public IReadOnlyDictionary<string, ParityAddress>? Branches { get; set; }

        [JsonPropertyName("custom_name")]
        public string? Renamed { get; set; }

        [JsonIgnore]
        public string? Ignored { get; set; }

        public byte[]? Payload { get; set; }

        public Uri? Link { get; set; }
    }

    [AsyncApi(DocumentName)]
    public sealed class ParityChannels
    {
        [Channel("parity/sent")]
        [SendOperation(typeof(ParityMessage), OperationId = "paritySent")]
        public void Sent()
        {
        }

        [Channel("parity/received")]
        [ReceiveOperation(typeof(ParityShape), OperationId = "parityReceived")]
        public void Received()
        {
        }
    }

    [Theory]
    [InlineData(AsyncApiVersion.AsyncApi3_0, "framework-parity.v3.json")]
    [InlineData(AsyncApiVersion.AsyncApi2_0, "framework-parity.v2.json")]
    public async Task GetAsyncApiDocument_MatchesGoldenOnEveryTargetFramework(AsyncApiVersion version, string goldenFileName)
    {
        // Arrange
        var goldenPath = Path.Combine(GoldenDirectory(), goldenFileName);

        // Act
        var actual = Normalize(await GetDocumentJsonAsync(version));

        // Assert
        if (Environment.GetEnvironmentVariable("BIELU_UPDATE_GOLDEN") == "1")
        {
            await File.WriteAllTextAsync(goldenPath, actual + "\n");
            return;
        }

        File.Exists(goldenPath).ShouldBeTrue($"Missing golden file {goldenPath}. Run with BIELU_UPDATE_GOLDEN=1 to create it.");
        var expected = Normalize(await File.ReadAllTextAsync(goldenPath));
        actual.ShouldBe(expected, $"The document generated on {RuntimeName()} differs from {goldenFileName}.");
    }

    private static async Task<string> GetDocumentJsonAsync(AsyncApiVersion version)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, options =>
        {
            options.AsyncApiVersion = version;
            options.WithInfo("Framework parity", "1.0.0");
            options.AddServer("broker", "localhost:9092", "kafka");
            // Channels without [AsyncApi(name)] land in every document; keep the golden file to this
            // fixture so unrelated tests can add channels without touching it.
            options.AddIncludedChannel("parity/sent");
            options.AddIncludedChannel("parity/received");
        });

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        var client = app.GetTestClient();
        var json = await client.GetStringAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", DocumentName));
        await app.StopAsync();

        return json;
    }

    /// <summary>
    /// Sorts object members, so the comparison covers what the document says and not the order its
    /// writer happens to emit members in. Array order is kept, because in <c>required</c>, <c>enum</c>
    /// and <c>oneOf</c> it can matter.
    /// </summary>
    private static string Normalize(string json) =>
        Sort(JsonNode.Parse(json))!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj
            .OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => KeyValuePair.Create(property.Key, Sort(property.Value)))),
        JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
        _ => node?.DeepClone(),
    };

    // From an assembly attribute rather than [CallerFilePath], which deterministic CI builds map to /_/.
    private static string GoldenDirectory() =>
        typeof(FrameworkParityDocumentTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "GoldenDirectory")
            .Value!;

    private static string RuntimeName() => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
}
