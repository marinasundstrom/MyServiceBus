using System;
using System.Reflection;

namespace MyServiceBus;

public static class NamingConventions
{
    static IMessageEntityNameFormatter _entityNameFormatter = new DefaultMessageEntityNameFormatter();

    public static IMessageEntityNameFormatter EntityNameFormatter => _entityNameFormatter;

    public static void SetEntityNameFormatter(IMessageEntityNameFormatter formatter)
    {
        _entityNameFormatter = formatter;
    }

    public static string GetMessageUrn(Type messageType)
    {
        return $"urn:message:{GetMessageName(messageType)}";
    }

    [Throws(typeof(AmbiguousMatchException))]
    public static string GetExchangeName(Type messageType)
    {
        var attr = messageType.GetCustomAttribute<EntityNameAttribute>();
        if (attr != null)
            return attr.EntityName;

        return _entityNameFormatter.FormatEntityName(messageType);
    }

    public static string GetMessageName(Type messageType)
    {
        return $"{messageType.Namespace}:{messageType.Name}";
    }

    public static string GetQueueName(Type messageType)
    {
        return $"{messageType.Name.ToLower().Replace('.', '-')}2-consumer";
    }

    class DefaultMessageEntityNameFormatter : IMessageEntityNameFormatter
    {
        public string FormatEntityName(Type messageType)
        {
            return GetMessageName(messageType);
        }
    }
}