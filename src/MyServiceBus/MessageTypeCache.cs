using System;
using System.Collections.Generic;
using System.Linq;

namespace MyServiceBus;

public static class MessageTypeCache
{
    public static bool IsContractType(Type type) => type.Namespace != "System"
        && !(type.Namespace?.StartsWith("System.", StringComparison.Ordinal) ?? false);

    public static Type[] GetMessageTypes(Type messageType)
    {
        if (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(Batch<>))
        {
            var inner = messageType.GetGenericArguments()[0];
            return new[] { messageType, inner };
        }

        var types = new List<Type> { messageType };
        for (var baseType = messageType.BaseType; baseType is not null && baseType != typeof(object); baseType = baseType.BaseType)
            if (IsContractType(baseType))
                types.Add(baseType);

        types.AddRange(messageType.GetInterfaces().Where(IsContractType).OrderBy(type => type.FullName, StringComparer.Ordinal));
        return types.Distinct().ToArray();
    }
}
