namespace MyServiceBus;

public enum SchedulingPlacement
{
    ProcessLocal,
    BrokerNative,
    Embedded,
    RemoteService,
    TransactionalOutbox
}
