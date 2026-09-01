namespace TestApp;

public sealed record ParallelOrderChecksRequested(Guid OrderId);

public sealed record PaymentCheckRequested(Guid OrderId);

public sealed record InventoryCheckRequested(Guid OrderId);
