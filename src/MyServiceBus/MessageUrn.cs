using System;

namespace MyServiceBus;

public static class MessageUrn
{
    public static string For(Type messageType)
    {
        if (messageType.IsGenericType)
        {
            var genericType = messageType.GetGenericTypeDefinition();
            var name = genericType.Name.Split('`')[0];
            var arguments = string.Join(",", messageType.GetGenericArguments().Select(FormatType));
            var messageNamespace = genericType == typeof(Fault<>) ? "MassTransit" : genericType.Namespace;
            return $"urn:message:{messageNamespace}:{name}[[{arguments}]]";
        }

        return $"urn:message:{messageType.Namespace}:{messageType.Name}";
    }

    private static string FormatType(Type messageType)
    {
        return $"{messageType.Namespace}:{messageType.Name}";
    }
}
