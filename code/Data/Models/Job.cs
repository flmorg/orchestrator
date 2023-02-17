using Data.Enums;

namespace Data.Models;

public sealed class Job
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public JobStatus Status { get; set; }

    public string QueueName { get; set; }

    public IList<Trigger> Triggers { get; set; }

    public override int GetHashCode()
    {
        return Id.GetHashCode()
            + Name.GetHashCode()
            + Status.GetHashCode()
            + QueueName.GetHashCode()
            + Triggers.Select(x => x.GetHashCode()).Sum();
    }

    public override bool Equals(object? obj)
    {
        return obj is not null && obj is Job job
            && Id == job.Id
            && Name == job.Name
            && Status == job.Status
            && QueueName == job.QueueName
            && TriggersMatch(job.Triggers);
    }

    private bool TriggersMatch(IList<Trigger> triggers)
    {
        if (Triggers.Count != triggers.Count)
        {
            return false;
        }

        foreach (Trigger trigger in triggers)
        {
            Trigger? oldTrigger = Triggers
                .FirstOrDefault(x => x.Id == trigger.Id);

            if (!trigger.Equals(oldTrigger))
            {
                return false;
            }
        }

        return true;
    }
}