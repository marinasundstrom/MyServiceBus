using System;
using System.Reflection;

namespace MyServiceBus;

public static class EntityNameFormatter
{
    static IMessageEntityNameFormatter _formatter = new DefaultMessageEntityNameFormatter();

    public static IMessageEntityNameFormatter Formatter => _formatter;

    public static void SetFormatter(IMessageEntityNameFormatter formatter)
    {
        _formatter = formatter;
    }

    public static string Format(Type messageType) => Format(messageType, null);

    public static string Format(Type messageType, IMessageEntityNameFormatter? formatter)
    {
        var attr = messageType.GetCustomAttribute<EntityNameAttribute>();
        if (attr != null)
            return attr.EntityName;

        return (formatter ?? _formatter).FormatEntityName(messageType);
    }

    class DefaultMessageEntityNameFormatter : IMessageEntityNameFormatter
    {
        public string FormatEntityName(Type messageType)
        {
            return $"{messageType.Namespace}:{messageType.Name}";
        }
    }
}
