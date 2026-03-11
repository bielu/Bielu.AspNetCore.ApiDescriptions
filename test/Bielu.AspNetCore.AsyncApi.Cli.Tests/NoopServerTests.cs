using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Microsoft.AspNetCore.Http.Features;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Unit tests for NoopServer.
/// </summary>
public class NoopServerTests
{
    [Fact]
    public void Features_ReturnsEmptyFeatureCollection()
    {
        // Arrange
        using var server = new NoopServer();

        // Assert
        server.Features.ShouldNotBeNull();
        server.Features.ShouldBeOfType<FeatureCollection>();
    }

    [Fact]
    public async Task StartAsync_CompletesImmediately()
    {
        // Arrange
        using var server = new NoopServer();

        // Act & Assert - should not throw
        await server.StartAsync<object>(null!, CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CompletesImmediately()
    {
        // Arrange
        using var server = new NoopServer();

        // Act & Assert - should not throw
        await server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var server = new NoopServer();

        // Act & Assert
        Should.NotThrow(() => server.Dispose());
    }
}
