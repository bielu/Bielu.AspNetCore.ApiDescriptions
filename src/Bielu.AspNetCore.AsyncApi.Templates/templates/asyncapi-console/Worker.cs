using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsyncApiConsole;

[AsyncApi]
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            
            // Simulate processing a message
            ProcessMessage(new TaskMessage("Process something", DateTime.Now));

            await Task.Delay(1000, stoppingToken);
        }
    }

    /// <summary>
    /// Processes an incoming task message.
    /// </summary>
    /// <param name="message">The task message to process.</param>
    [Channel("tasks/incoming")]
    [PublishOperation(typeof(TaskMessage), "ProcessTask", Summary = "Process an incoming task.")]
    public void ProcessMessage(TaskMessage message)
    {
        _logger.LogInformation("Processing task: {Description}", message.Description);
    }
}

/// <summary>
/// Represents a task message.
/// </summary>
/// <param name="Description">Description of the task.</param>
/// <param name="CreatedAt">When the task was created.</param>
public record TaskMessage(string Description, DateTime CreatedAt);
