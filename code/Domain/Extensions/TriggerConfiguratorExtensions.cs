using Common.Configuration;
using Domain.Features.PriceGrabber.Jobs;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace Domain.Extensions;

public static class TriggerConfiguratorExtensions
{
    public static ITriggerConfigurator Schedule<T>(
        this ITriggerConfigurator triggerConfigurator,
        IHostEnvironment hostEnvironment,
        ScheduleConfig config)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return triggerConfigurator
                .StartNow()
                .WithSimpleSchedule(x => x.WithMisfireHandlingInstructionIgnoreMisfires());
        }

        if (typeof(T) == typeof(PriceGrabberJob))
        {
            return triggerConfigurator
                .WithCronSchedule(config.PriceGrabberTrigger);
        }

        throw new NotSupportedException(typeof(T).Name);
    }
}
