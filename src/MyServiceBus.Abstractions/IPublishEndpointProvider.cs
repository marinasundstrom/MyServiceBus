namespace MyServiceBus;

public interface IPublishEndpointProvider
{
    IPublishEndpoint GetPublishEndpoint();
}
