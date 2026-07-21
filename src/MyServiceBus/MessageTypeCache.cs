using System;
using System.Collections.Generic;
using System.Linq;

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

        var types = new List<Type> { messageType };
        for (var baseType = messageType.BaseType; baseType is not null && baseType != typeof(object); baseType = baseType.BaseType)
            types.Add(baseType);

        types.AddRange(messageType.GetInterfaces().OrderBy(type => type.FullName, StringComparer.Ordinal));
        return types.Distinct().ToArray();
    }
}
