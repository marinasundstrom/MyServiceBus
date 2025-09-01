using System;

namespace MyServiceBus;

public static class MessageTypeCache
{
    public static Type[] GetMessageTypes(Type messageType)
    {
        if (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(Batch<>))
        {
            var inner = messageType.GetGenericArguments()[0];
            return new[] { messageType, inner };
        }

        return new[] { messageType };
    }
}
