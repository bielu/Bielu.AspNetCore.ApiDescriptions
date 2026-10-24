using System.Diagnostics;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Fixtures;

/// <summary>
/// A <see cref="FactAttribute"/> that skips, rather than fails, when no container runtime is reachable: Aspire
/// runs resources such as the Kafka broker as containers. CI's Linux runners have Docker; a developer machine
/// without a running Docker daemon simply reports these tests as skipped.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = "Requires a running Docker daemon.";
        }
    }
}

internal static class DockerAvailability
{
    private static readonly Lazy<bool> _isAvailable = new(Probe);

    public static bool IsAvailable => _isAvailable.Value;

    private static bool Probe()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process is null)
            {
                return false;
            }

            // Drain both streams so a chatty daemon cannot block the probe on a full pipe.
            _ = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            // No docker CLI on the PATH.
            return false;
        }
    }
}
