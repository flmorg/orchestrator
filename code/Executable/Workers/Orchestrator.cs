using Data.DbContexts;
using Domain.Services.Interfaces;
using FLM.RabbitMQ.Configuration;
using FLM.RabbitMQ.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Executable.Workers;

internal sealed class Orchestrator : BackgroundService
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker is starting");

        await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
        {
            OrchestratorContext orchestratorContext = scope.ServiceProvider.GetRequiredService<OrchestratorContext>();

            _logger.LogInformation(
                "Pending migrations: {Count}",
                orchestratorContext.Database.GetPendingMigrations().Count());

            await orchestratorContext.Database.MigrateAsync(stoppingToken);
            string lastAppliedMigration = (await orchestratorContext.Database.GetAppliedMigrationsAsync(stoppingToken))
                .Last();

            _logger.LogInformation(
                "Current database version: {Version}",
                lastAppliedMigration);
        }

        await _schedulingService.CreateAndStartScheduler(stoppingToken);

        _rabbitMqConnection.ConfigureQueues(GetQueueConfigurations());

        while (!stoppingToken.IsCancellationRequested)
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

    private static List<QueueConfiguration> GetQueueConfigurations() => new()
    {
    };
}
