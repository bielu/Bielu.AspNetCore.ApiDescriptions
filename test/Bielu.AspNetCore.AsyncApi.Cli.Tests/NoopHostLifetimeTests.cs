using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Unit tests for NoopHostLifetime.
/// </summary>
public class NoopHostLifetimeTests
{
    [Fact]
    public async Task WaitForStartAsync_CompletesImmediately()
    {
        // Arrange
        var lifetime = new NoopHostLifetime();

        // Act & Assert - should not throw and complete immediately
        await lifetime.WaitForStartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CompletesImmediately()
    {
        // Arrange
        var lifetime = new NoopHostLifetime();

        // Act & Assert - should not throw and complete immediately
        await lifetime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WaitForStartAsync_WithCancelledToken_CompletesImmediately()
    {
        // Arrange
        var lifetime = new NoopHostLifetime();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should still complete since it's a no-op
        await lifetime.WaitForStartAsync(cts.Token);
    }
}
