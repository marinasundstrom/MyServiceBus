using System;
using System.Reflection;

namespace MyServiceBus;

public static class MessageUrn
{
    public static string For(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        if (messageType.GetCustomAttribute<MessageUrnAttribute>(inherit: false) is { } attribute)
            return attribute.Urn;
        if (messageType.IsGenericType)
        {
            var genericType = messageType.GetGenericTypeDefinition();
            var name = genericType.Name.Split('`')[0];
            var arguments = string.Join(",", messageType.GetGenericArguments().Select(FormatType));
            var messageNamespace = genericType == typeof(Fault<>) ? "MassTransit" : genericType.Namespace;
            return $"urn:message:{messageNamespace}:{name}[[{arguments}]]";
        }

        return $"urn:message:{messageType.Namespace}:{TypeName(messageType)}";
    }

    private static string TypeName(Type type) => type.DeclaringType is null
        ? type.Name : TypeName(type.DeclaringType) + "+" + type.Name;

    private static string FormatType(Type messageType)
    {
        var urn = For(messageType);
        return urn.StartsWith("urn:message:", StringComparison.Ordinal) ? urn[12..] : urn;
    }
}
