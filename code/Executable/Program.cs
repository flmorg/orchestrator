using Data.DbContexts;
using Executable.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Configure logging with Serilog
    builder.Logging.ClearProviders();
    builder.Host.UseSerilog();

    builder.Services
        .AddDbContext<OrchestratorContext>(optionsBuilder =>
        {
            ConfigurationManager configuration = builder.Configuration;
            string? connectionString = configuration.GetConnectionString("Orchestrator");

            optionsBuilder
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();

            if (builder.Environment.IsDevelopment())
            {
                optionsBuilder
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            }
        })
        .CustomConfigure(builder);

    WebApplication app = builder.Build();

    // Apply migrations at startup
    using (IServiceScope scope = app.Services.CreateScope())
    {
        OrchestratorContext dbContext = scope.ServiceProvider.GetRequiredService<OrchestratorContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseRouting();
    app.MapControllers();

    app.MapGet("/", () => "Hello, World!");

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Logger.Fatal(exception, "start failed");
}
finally
{
    await Log.CloseAndFlushAsync();
}
