using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Fixtures;

/// <summary>
/// Writes log entries to the xUnit test output. Aspire's testing builder forwards every resource's console output
/// to logging (category <c>&lt;app&gt;.Resources.&lt;name&gt;</c>), so a resource that fails to start shows why in the
/// failed test's output instead of only in a console logger CI does not print.
/// </summary>
internal sealed class TestOutputLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(output, categoryName);

    public void Dispose()
    {
    }

    private sealed class TestOutputLogger(ITestOutputHelper output, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                output.WriteLine($"[{logLevel}] {categoryName}: {formatter(state, exception)}{(exception is null ? "" : Environment.NewLine + exception)}");
            }
            catch (InvalidOperationException)
            {
                // The test already finished: xUnit no longer accepts output for it.
            }
        }
    }
}
