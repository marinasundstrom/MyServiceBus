using System;

namespace MyServiceBus;

[Flags]
public enum EndpointCapabilities
{
    None = 0,
    Acknowledgement = 1 << 0,
    Retry = 1 << 1,
    BatchSend = 1 << 2
}

