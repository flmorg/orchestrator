using Data.Enums;

namespace Data.Models;

public sealed class Trigger
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public Job Job { get; set; }

    public string CronExpression { get; set; }

    public TriggerStatus Status { get; set; }

    public override int GetHashCode()
    {
        return Id.GetHashCode()
            + JobId.GetHashCode()
            + CronExpression.GetHashCode()
            + Status.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return obj is not null && obj is Trigger trigger
            && Id == trigger.Id
            && JobId == trigger.JobId
            && CronExpression == trigger.CronExpression
            && Status == trigger.Status;
    }
}