namespace MyServiceBus;

public interface ISendEndpointProvider
{
    ISendEndpoint GetSendEndpoint(Uri uri);
}
