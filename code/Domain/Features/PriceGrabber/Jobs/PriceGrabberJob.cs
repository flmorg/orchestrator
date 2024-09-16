using Data.DbContexts;
using Data.Features.PriceGrabber.Enums;
using Data.Features.PriceGrabber.Models;
using Domain.Features.PriceGrabber.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Domain.Features.PriceGrabber.Jobs;

[DisallowConcurrentExecution]
public sealed class PriceGrabberJob : IJob
{
    private readonly ILogger _logger;
    private readonly OrchestratorContext _orchestratorContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public PriceGrabberJob(ILogger<PriceGrabberJob> logger, OrchestratorContext orchestratorContext, IPublishEndpoint publishEndpoint)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orchestratorContext = orchestratorContext ?? throw new ArgumentNullException(nameof(orchestratorContext));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            List<Product> products = await _orchestratorContext.Products
                .Where(x => x.State == ProductState.Scheduled)
                .ToListAsync();

            foreach (Product product in products)
            {
                await using IDbContextTransaction transaction = await _orchestratorContext.Database.BeginTransactionAsync();

                try
                {
                    product.LastRefreshedAt = DateTime.UtcNow;
                    product.State = ProductState.Processing;

                    await _publishEndpoint.Publish(new PriceGrabberRequest
                    {
                        TrackingId = Guid.NewGuid(),
                        ProductId = product.Id,
                        Url = product.Url
                    });

                    _logger.LogInformation("product processing | id {Id}", product.Id);

                    await _orchestratorContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("product processing failed | id {Id}", product.Id);
                    throw;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "publish failed | job {Job}", context.JobDetail.Key.Name);
        }
    }
}
