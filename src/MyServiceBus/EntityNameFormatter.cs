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

    [Throws(typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public static string Format(Type messageType)
    {
        var attr = messageType.GetCustomAttribute<EntityNameAttribute>();
        if (attr != null)
            return attr.EntityName;

        return _formatter.FormatEntityName(messageType);
    }

    class DefaultMessageEntityNameFormatter : IMessageEntityNameFormatter
    {
        public string FormatEntityName(Type messageType)
        {
            return $"{messageType.Namespace}:{messageType.Name}";
        }
    }
}
