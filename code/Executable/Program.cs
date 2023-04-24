using Executable.Extensions;
using FLM.Serilog.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .CreateDefaultBootstrapLogger();

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
}
finally
{
    await Log.CloseAndFlushAsync();
}
