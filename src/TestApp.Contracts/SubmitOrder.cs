namespace TestApp;

public record SubmitOrder
{
    public required Guid OrderId { get; init; }

    public string Message { get; init; }
}
