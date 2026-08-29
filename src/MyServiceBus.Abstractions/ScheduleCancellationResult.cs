namespace MyServiceBus;

public enum ScheduleCancellationResult
{
    Cancelled,
    AlreadyCancelled,
    TooLate,
    NotScheduled,
    NotFound
}
