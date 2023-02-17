using Data.DbContexts;
using Domain.Services.Interfaces;
using FLM.RabbitMQ.Configuration;
using FLM.RabbitMQ.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Executable.Workers;

internal class Orchestrator : BackgroundService
{
    private readonly ILogger<Orchestrator> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISchedulingService _schedulingService;
    private readonly IRabbitMQConnection _rabbitMqConnection;

    public Orchestrator(
        ILogger<Orchestrator> logger,
        IServiceProvider serviceProvider,
        ISchedulingService schedulingService,
        IRabbitMQConnection rabbitMqConnection)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
        _rabbitMqConnection = rabbitMqConnection ?? throw new ArgumentNullException(nameof(rabbitMqConnection));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker is starting");

        await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
        {
            OrchestratorContext orchestratorContext = scope.ServiceProvider.GetRequiredService<OrchestratorContext>();
            await orchestratorContext.Database.MigrateAsync(cancellationToken);
        }

        await _schedulingService.CreateAndStartScheduler(cancellationToken);

        _rabbitMqConnection.ConfigureQueues(GetQueueConfigurations());

        while (!cancellationToken.IsCancellationRequested)
        {
            await _schedulingService.Schedule();
            await Task.Delay(10_000, CancellationToken.None);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker is stopping");

        await _schedulingService.ShutdownScheduler(cancellationToken);

        await base.StopAsync(cancellationToken);
    }

    private static IEnumerable<QueueConfiguration> GetQueueConfigurations()
    {
        // Return the queue configurations that you need.
        return new List<QueueConfiguration>
        {
            new()
            {
                QueueName = "test",
                MessageType = typeof(string)
            }
        };
    }
}