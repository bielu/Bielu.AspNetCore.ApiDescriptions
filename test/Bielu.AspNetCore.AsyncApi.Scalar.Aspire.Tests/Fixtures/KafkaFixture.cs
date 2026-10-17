using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Fixtures;

/// <summary>
/// Starts one Kafka broker through Aspire for every test in a class. Only the broker runs as a container;
/// the code under test runs in the test process, so coverage is collected for it as usual.
/// </summary>
public sealed class KafkaFixture : IAsyncLifetime
{
    private const string ResourceName = "kafka";

    private DistributedApplication? _app;

    /// <summary>The broker's bootstrap servers, once started.</summary>
    public string BootstrapServers { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // The DockerFact tests in the class are skipped in this case; don't fail the fixture for them.
        if (!DockerAvailability.IsAvailable)
        {
            return;
        }

        // A first run may need to pull the broker image.
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        var builder = DistributedApplicationTestingBuilder.Create();
        builder.AddKafka(ResourceName);

        _app = builder.Build();
        await _app.StartAsync(timeout.Token);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync(ResourceName, timeout.Token);

        BootstrapServers = await _app.GetConnectionStringAsync(ResourceName, timeout.Token)
            ?? throw new InvalidOperationException("The Kafka resource did not report a connection string.");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
