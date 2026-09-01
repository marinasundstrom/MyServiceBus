namespace TestApp;

public sealed record OrderOrchestrationStarted(Guid OrderId);

public sealed record OrchestrationInventoryRequested(Guid OrderId);

public sealed record OrchestrationInventoryReserved(Guid OrderId);

public sealed record OrchestrationPaymentRequested(Guid OrderId);

public sealed record OrchestrationPaymentCaptured(Guid OrderId);

public sealed record OrderOrchestrationCompleted(Guid OrderId);
