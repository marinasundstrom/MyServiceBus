namespace MyServiceBus;

public interface ISendEndpointProvider
{
    Task<ISendEndpoint> GetSendEndpoint(Uri uri);
}
