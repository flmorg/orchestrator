using System.Globalization;
using Data.DbContexts;
using Domain.Jobs;
using Domain.Services;
using Domain.Services.Interfaces;
using Executable.Workers;
using FLM.RabbitMQ.Configuration;
using FLM.RabbitMQ.Core;
using FLM.RabbitMQ.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Exceptions;

namespace Executable.Extensions;

internal static class HostingExtensions
{
    public static IHostBuilder Configure(this IHostBuilder hostBuilder) =>
        hostBuilder
            .ConfigureServices((context, services) =>
            {
                IConfiguration configuration = context.Configuration;
                services
                    .Configure<RabbitMQConfiguration>(configuration.GetSection(nameof(RabbitMQConfiguration)))
                    .AddLogging(configure => configure.AddSerilog())
                    .AddQuartzHostedService(options => options.WaitForJobsToComplete = true)
                    .AddQuartz(q =>
                    {
                        // https://www.quartz-scheduler.net/documentation/quartz-3.x/packages/microsoft-di-integration.html#di-aware-job-factories
                        q.UseMicrosoftDependencyInjectionJobFactory();
                        q.UseSimpleTypeLoader();
                        q.UseInMemoryStore();
                        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
                    })
                    .AddDbContext<OrchestratorContext>(optionsBuilder =>
                    {
                        string? connectionString = configuration.GetConnectionString("Orchestrator");

                        optionsBuilder
                            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                            .UseSnakeCaseNamingConvention();

                        if (!context.HostingEnvironment.IsDevelopment())
                        {
                            return;
                        }

                        optionsBuilder
                            .EnableSensitiveDataLogging()
                            .EnableDetailedErrors();
                    })

                    .AddTransient<PubMessageJob>()
                    .AddHostedService<Orchestrator>()
                    .AddSingleton<IRabbitMQConnection, RabbitMQConnection>()
                    .AddSingleton<ISchedulingService, SchedulingService>();
            })
            .ConfigureLogging(builder => builder.ClearProviders())
            .UseSerilog((context, loggerConfiguration) =>
            {
                loggerConfiguration
                    .WriteTo.Async(
                        configure: sinkConfiguration => sinkConfiguration.Console(formatProvider: CultureInfo.InvariantCulture),
                        blockWhenFull: true)
                    .Enrich.WithEnvironmentName()
                    .Enrich.WithThreadId()
                    .Enrich.WithProcessId()
                    .Enrich.WithExceptionDetails()
                    .Enrich.WithSpan();

                Dictionary<string, LogEventLevel> logLevelOverrides;

                if (context.HostingEnvironment.IsDevelopment())
                {
                    loggerConfiguration.MinimumLevel.Debug();

                    logLevelOverrides = new()
                    {
                        { "Microsoft.EntityFrameworkCore", LogEventLevel.Warning },
                        { "Quartz", LogEventLevel.Warning }
                    };
                }
                else
                {
                    loggerConfiguration.MinimumLevel.Information();

                    logLevelOverrides = new()
                    {
                        { "System", LogEventLevel.Warning },
                        { "Microsoft", LogEventLevel.Warning },
                        { "Microsoft.Hosting.Lifetime", LogEventLevel.Information },
                        { "Microsoft.EntityFrameworkCore", LogEventLevel.Warning },
                        { "Quartz", LogEventLevel.Warning }
                    };
                }

                foreach (KeyValuePair<string, LogEventLevel> pair in logLevelOverrides)
                {
                    loggerConfiguration.MinimumLevel.Override(pair.Key, pair.Value);
                }
            });
}
