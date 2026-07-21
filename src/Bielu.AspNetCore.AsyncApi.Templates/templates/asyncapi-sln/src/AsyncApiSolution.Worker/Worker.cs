using AsyncApiSolution.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsyncApiSolution.Worker;

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
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            
            // Simulate processing
            var message = new SystemMessage("Periodic heartbeat", DateTime.Now);
            _logger.LogInformation("Processing message: {Content}", message.Content);

            await Task.Delay(5000, stoppingToken);
        }
    }
}
