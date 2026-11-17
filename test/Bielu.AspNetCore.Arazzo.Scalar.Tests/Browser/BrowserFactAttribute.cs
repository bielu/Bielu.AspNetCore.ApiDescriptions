using Microsoft.Playwright;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Browser;

/// <summary>
/// A <see cref="FactAttribute"/> that skips, rather than fails, when no browser Playwright can drive is
/// installed. CI installs Playwright's Chromium (<c>playwright.ps1 install chromium</c>); on a developer
/// machine an installed Google Chrome is used instead, and without either the tests report as skipped,
/// as <c>[DockerFact]</c> does without Docker.
/// </summary>
public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute()
    {
        if (BrowserAvailability.LaunchOptions is null)
        {
            Skip = "Requires Playwright's Chromium (pwsh bin/Debug/net10.0/playwright.ps1 install chromium) or Google Chrome.";
        }
    }
}

internal static class BrowserAvailability
{
    private static readonly Lazy<BrowserTypeLaunchOptions?> _launchOptions = new(Probe);

    /// <summary>How to launch a browser that works here, or <see langword="null"/> when none does.</summary>
    public static BrowserTypeLaunchOptions? LaunchOptions => _launchOptions.Value;

    private static BrowserTypeLaunchOptions? Probe()
    {
        // Playwright's own Chromium first (what CI installs), then the machine's Google Chrome.
        BrowserTypeLaunchOptions[] candidates = [new() { Headless = true }, new() { Headless = true, Channel = "chrome" }];
        try
        {
            // Attribute constructors cannot await; this runs once per test run.
            using var playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
            foreach (var options in candidates)
            {
                try
                {
                    var browser = playwright.Chromium.LaunchAsync(options).GetAwaiter().GetResult();
                    browser.CloseAsync().GetAwaiter().GetResult();
                    return options;
                }
                catch (PlaywrightException)
                {
                    // Not installed for this Playwright version: try the next one.
                }
            }
        }
        catch (PlaywrightException)
        {
            // The Playwright driver itself could not start.
        }

        return null;
    }
}
