namespace MyServiceBus;

public static class NamingHelpers
{
    public static string GetQueueName(Type consumerType)
    {
        var name = consumerType.Name;

        if (name.EndsWith("Consumer"))
            name = name[..^"Consumer".Length];

        return consumerType.GetInterfaces().First().GetGenericArguments().First().Name; //name.ToKebabCase() + "-consumer";
    }

    public static string GetExchangeName(Type messageType)
    {
        return $"{messageType.Namespace}:{messageType.Name!}"; // e.g. Contracts.Messages.SubmitOrder
    }

    public static string GetRoutingKey(Type messageType)
    {
        return messageType.FullName!;
    }
}
