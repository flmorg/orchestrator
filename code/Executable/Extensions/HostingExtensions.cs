using Common.Configuration;
using Data.DbContexts;
using Domain.Extensions;
using Domain.Features.PriceGrabber.Jobs;
using Domain.Features.PriceGrabber.Services;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Executable.Extensions;

internal static class HostingExtensions
{
    public static IServiceCollection CustomConfigure(this IServiceCollection services, WebApplicationBuilder context)
    {
        IConfiguration configuration = context.Configuration;

        services.AddControllers();

        return services
            .AddQuartz(q =>
            {
                // https://www.quartz-scheduler.net/documentation/quartz-3.x/packages/microsoft-di-integration.html#di-aware-job-factories
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);

                ScheduleConfig? scheduleConfig = configuration
                    .GetRequiredSection(nameof(ScheduleConfig))
                    .Get<ScheduleConfig>();

                if (scheduleConfig is null)
                {
                    throw new ArgumentException(nameof(scheduleConfig));
                }

                JobKey priceGrabberJobKey = new(nameof(PriceGrabberJob));
                q.AddJob<PriceGrabberJob>(opts => opts.WithIdentity(priceGrabberJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(priceGrabberJobKey)
                    .WithIdentity(nameof(PriceGrabberJob))
                    .Schedule<PriceGrabberJob>(context.Environment, scheduleConfig));
            })
            .AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
                options.AwaitApplicationStarted = true;
            })
            .AddMassTransit(x =>
            {
                RabbitMqConfig? rabbitmqConfig = configuration
                    .GetRequiredSection(nameof(RabbitMqConfig))
                    .Get<RabbitMqConfig>();

                if (rabbitmqConfig is null)
                {
                    throw new ArgumentException(nameof(rabbitmqConfig));
                }

                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Host(rabbitmqConfig.Host, rabbitmqConfig.VirtualHost, h =>
                    {
                        h.Username(rabbitmqConfig.Username);
                        h.Password(rabbitmqConfig.Password);
                    });
                });
            })
            .AddDbContext<OrchestratorContext>(optionsBuilder =>
            {
                string? connectionString = configuration.GetConnectionString("Orchestrator");

                optionsBuilder
                    .UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention();

                if (!context.Environment.IsDevelopment())
                {
                    return;
                }

                optionsBuilder
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            })
            .AddScoped<PriceGrabberJob>()
            .AddScoped<PriceGrabberService>();
    }
}
