using Common.Constants;
using FLM.RabbitMQ.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Domain.Jobs;

public sealed class PubMessageJob : IJob
{
    private readonly ILogger _logger;
    private readonly IRabbitMQConnection _rabbitMqConnection;

    public PubMessageJob(ILogger<PubMessageJob> logger, IRabbitMQConnection rabbitMqConnection)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rabbitMqConnection = rabbitMqConnection ?? throw new ArgumentNullException(nameof(rabbitMqConnection));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        string? queue = context.JobDetail.JobDataMap
            .GetString(GeneralConstants.JOB_QUEUE_KEY)
            ?.Trim();

        string jobId = context.JobDetail.Key.Name;

        if (string.IsNullOrEmpty(queue))
        {
            _logger.LogCritical("Received empty queue name for job {JobId}", jobId);
            return;
        }

        try
        {
            _rabbitMqConnection.PublishMessage(queue, string.Empty);
            _logger.LogInformation("Published message on queue {Queue}", queue);

            await Task.CompletedTask;
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Failed to publish message on queue {Queue} for job {JobId}",
                queue,
                jobId);
        }
    }
}