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

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;
        _ = services
            .Configure<RabbitMQConfiguration>(configuration.GetSection(nameof(RabbitMQConfiguration)))

            .AddDbContext<OrchestratorContext>(optionsBuilder =>
            {
                string? connectionString = configuration.GetConnectionString("Orchestrator");

                _ = optionsBuilder
                    .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                    .UseSnakeCaseNamingConvention();

                if (context.HostingEnvironment.IsDevelopment())
                {
                    _ = optionsBuilder
                        .LogTo(Console.WriteLine, LogLevel.Trace)
                        .EnableSensitiveDataLogging()
                        .EnableDetailedErrors();
                }
            })

            // https://www.quartz-scheduler.net/documentation/quartz-3.x/packages/microsoft-di-integration.html#di-aware-job-factories
            .AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
            })

            .AddTransient<PubMessageJob>()

            .AddHostedService<Orchestrator>()
            .AddQuartzHostedService(options => options.WaitForJobsToComplete = true)
            .AddSingleton<IRabbitMQConnection, RabbitMQConnection>()
            .AddSingleton<ISchedulingService, SchedulingService>();
    })
    .Build();

host.Run();