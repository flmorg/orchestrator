using System.Globalization;
using Common.Constants;
using Data.DbContexts;
using Data.Enums;
using Data.Models;
using Domain.Jobs;
using Domain.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Domain.Services;

public sealed class SchedulingService : ISchedulingService
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private IScheduler? _scheduler;
    private List<Job> _cachedJobs = new();

    public SchedulingService(
        ILogger<SchedulingService> logger,
        IServiceProvider serviceProvider,
        ISchedulerFactory schedulerFactory,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
    }

    public async Task CreateAndStartScheduler(CancellationToken cancellationToken)
    {
        _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        await _scheduler.Start(cancellationToken);
    }

    public async Task ShutdownScheduler(CancellationToken cancellationToken)
    {
        if (_scheduler is null)
        {
            return;
        }

        await _scheduler.Shutdown(cancellationToken);
    }

    public async Task Schedule()
    {
        await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
        {
            OrchestratorContext orchestratorContext = scope.ServiceProvider.GetRequiredService<OrchestratorContext>();

            List<Job> databaseJobs = await orchestratorContext.Jobs
                .Include(job => job.Triggers)
                .Where(job => job.Status == JobStatus.Enabled)
                .Where(job => job.Triggers.Any(trigger => trigger.Status == TriggerStatus.Enabled))
                .AsNoTracking()
                .ToListAsync();

            await ScheduleJobs(orchestratorContext, databaseJobs);
            await RescheduleJobs(orchestratorContext, databaseJobs);
            await DeleteJobs(databaseJobs);
        }
    }

    private async Task ScheduleJobs(OrchestratorContext orchestratorContext, IEnumerable<Job> databaseJobs)
    {
        List<Job> createdJobs = databaseJobs
            .Where(job => _cachedJobs.All(x => x.Id != job.Id))
            .ToList();

        foreach (Job job in createdJobs)
        {
            await ScheduleJob(orchestratorContext, job, false);
        }
    }

    private async Task ScheduleJob(OrchestratorContext orchestratorContext, Job job, bool isRescheduled)
    {
        if (_scheduler is null || !ShouldScheduleJob(job))
        {
            return;
        }

        string jobId = job.Id.ToString();
        JobKey jobKey = new(jobId);

        if (await _scheduler.CheckExists(jobKey))
        {
            _logger.LogCritical(
                "Found existing job with id {Id} while scheduling. Scheduling aborted",
                jobId);
            return;
        }

        _logger.LogInformation("Scheduling job {JobId}", jobId);

        IJobDetail jobDetail = JobBuilder
            .Create(typeof(PubMessageJob))
            .WithIdentity(jobId)
            .UsingJobData(GeneralConstants.JobQueueKey, job.QueueName)
            .StoreDurably()
            .Build();

        await _scheduler.AddJob(jobDetail, true);

        if (_hostEnvironment.IsDevelopment())
        {
            // schedule to run now, once
            ITrigger jobTrigger = TriggerBuilder
                .Create()
                .WithIdentity(Guid.Empty.ToString(), jobId)
                .WithSimpleSchedule()
                .ForJob(jobDetail)
                .Build();

            _ = await _scheduler.ScheduleJob(jobTrigger);
        }
        else
        {
            foreach (Trigger trigger in job.Triggers)
            {
                string triggerId = trigger.Id.ToString();

                if (!CronExpression.IsValidExpression(trigger.CronExpression))
                {
                    Trigger faultyTrigger = await orchestratorContext.Triggers
                        .FirstAsync(x => x.Id == trigger.Id);
                    faultyTrigger.Status = TriggerStatus.Disabled;

                    await orchestratorContext.SaveChangesAsync();
                    _logger.LogError(
                        "Found invalid trigger {TriggerId}. Trigger was disabled",
                        triggerId);

                    continue;
                }

                if (trigger.Status == TriggerStatus.Disabled)
                {
                    continue;
                }

                ITrigger jobTrigger = TriggerBuilder
                    .Create()
                    .WithIdentity(triggerId, jobId)
                    .StartNow()
                    .WithCronSchedule(
                        trigger.CronExpression,
                        x => x.WithMisfireHandlingInstructionFireAndProceed().InTimeZone(TimeZoneInfo.Utc))
                    .ForJob(jobDetail)
                    .Build();

                _ = await _scheduler.ScheduleJob(jobTrigger);

                _logger.LogInformation(
                    "Scheduled trigger {TriggerId} with next fire time at {Date}",
                    triggerId,
                    jobTrigger.GetNextFireTimeUtc()?.ToString(GeneralConstants.TriggerDateTimeFormat, CultureInfo.InvariantCulture));
            }
        }

        if (isRescheduled)
        {
            int index = _cachedJobs.FindIndex(x => x.Id == job.Id);
            _cachedJobs[index] = job;

            return;
        }

        _cachedJobs.Add(job);
    }

    private async Task RescheduleJobs(OrchestratorContext orchestratorContext, IEnumerable<Job> databaseJobs)
    {
        List<Job> updatedJobs = databaseJobs
            .Where(job => _cachedJobs.Any(x => x.Id == job.Id))
            .ToList();

        foreach (Job job in updatedJobs)
        {
            await RescheduleJob(orchestratorContext, job);
        }
    }

    private async Task RescheduleJob(OrchestratorContext orchestratorContext, Job job)
    {
        Job cachedJob = _cachedJobs
            .First(x => x.Id == job.Id);

        if (!ShouldRescheduleJob(job, cachedJob))
        {
            return;
        }

        await DeleteJob(job, true);
        await ScheduleJob(orchestratorContext, job, true);

        _logger.LogInformation("Rescheduled job {JobId}", job.Id.ToString());
    }

    private async Task DeleteJobs(IEnumerable<Job> databaseJobs)
    {
        List<Job> deletedJobs = _cachedJobs
            .Where(job => databaseJobs.All(x => x.Id != job.Id))
            .ToList();

        foreach (Job job in deletedJobs)
        {
            await DeleteJob(job, false);
        }
    }

    private async Task DeleteJob(Job job, bool isRescheduled)
    {
        if (_scheduler is null)
        {
            return;
        }

        _ = await _scheduler.DeleteJob(new JobKey(job.Id.ToString()));

        _cachedJobs = _cachedJobs
            .Where(x => x.Id != job.Id)
            .ToList();

        if (!isRescheduled)
        {
            _logger.LogInformation("Deleted job {JobId}", job.Id.ToString());
        }
    }

    private static bool ShouldScheduleJob(Job job) => job.Status == JobStatus.Enabled && job.Triggers.Any();

    private static bool ShouldRescheduleJob(Job updatedJob, Job cachedJob) => !updatedJob.Equals(cachedJob);
}
