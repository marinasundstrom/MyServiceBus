using System;

namespace MyServiceBus;

public class UnknownMessageTypeException : Exception
{
    public UnknownMessageTypeException(string? messageType)
        : base($"Unknown message type: {messageType ?? "<null>"}")
    {
    }
}
