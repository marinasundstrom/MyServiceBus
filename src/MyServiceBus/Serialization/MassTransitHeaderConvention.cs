using System;

namespace MyServiceBus.Serialization;

public sealed class MassTransitHeaderConvention : IMessageHeaderConvention
{
    public static MassTransitHeaderConvention Instance { get; } = new();

    private MassTransitHeaderConvention()
    {
    }

    public string ContentTypeHeader => "content_type";

    public string FaultAddressHeader => MessageHeaders.FaultAddress;

    public bool IsHostHeader(string headerName)
        => headerName.StartsWith("MT-Host-", StringComparison.Ordinal);
}
