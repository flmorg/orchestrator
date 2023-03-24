using Executable.Extensions;
using Serilog;
using Serilog.Formatting.Json;

#pragma warning disable CA1852 // Seal internal types
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter(renderMessage: true))
    .CreateBootstrapLogger();
#pragma warning restore CA1852 // Seal internal types

try
{
    IHost host = Host.CreateDefaultBuilder(args)
        .Configure()
        .Build();

    await host.RunAsync();
}
catch (Exception exception)
{
    Log.Logger.Fatal(exception, "Application failed to start");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
