namespace Common.Configuration;

public sealed record ScheduleConfig
{
    public required string PriceGrabberTrigger { get; init; }
}
