namespace TestApp;

public sealed record FulfillmentRequested(Guid OrderId);

public sealed record InventoryReservationRequested(Guid OrderId);

public sealed record InventoryReserved(Guid OrderId);

public sealed record FulfillmentCompleted(Guid OrderId);
