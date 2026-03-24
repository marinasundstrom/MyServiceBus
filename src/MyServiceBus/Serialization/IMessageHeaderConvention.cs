namespace MyServiceBus.Serialization;

public interface IMessageHeaderConvention
{
    string ContentTypeHeader { get; }

    string FaultAddressHeader { get; }

    bool IsHostHeader(string headerName);
}
