namespace MyServiceBus;

public static class NamingConventions
{
    public static string GetMessageUrn(Type messageType)
    {
        return $"urn:message:{GetMessageName(messageType)}";
    }

    public static string GetExchangeName(Type messageType)
    {
        return GetMessageName(messageType);
    }

    public static string GetMessageName(Type messageType)
    {
        return $"{messageType.Namespace}:{messageType.Name}";
    }

    public static string GetQueueName(Type messageType)
    {
        return $"{messageType.Name.ToLower().Replace('.', '-')}-consumer";
    }
}