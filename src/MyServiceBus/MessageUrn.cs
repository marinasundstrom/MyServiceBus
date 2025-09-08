using System;

namespace MyServiceBus;

public static class MessageUrn
{
    public static string For(Type messageType)
    {
        return $"urn:message:{messageType.Namespace}:{messageType.Name}";
    }
}
