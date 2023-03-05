namespace Domain.Services.Interfaces;

public interface ISchedulingService
{
    Task CreateAndStartScheduler(CancellationToken cancellationToken);

    Task ShutdownScheduler(CancellationToken cancellationToken);

    Task Schedule();
}