namespace MyServiceBus.RabbitMq.Tests;

public class ContractNamingTests
{
    public record NamingMessage(string Value);
    private sealed class Formatter(string prefix) : IMessageEntityNameFormatter
    {
        public string FormatEntityName(Type type) => prefix + type.Name;
    }

    [Fact]
    public void Entity_formatters_are_local_to_each_bus()
    {
        var original = EntityNameFormatter.Format(typeof(NamingMessage));
        var first = new RabbitMqFactoryConfigurator();
        var second = new RabbitMqFactoryConfigurator();
        first.SetEntityNameFormatter(new Formatter("first-"));
        second.SetEntityNameFormatter(new Formatter("second-"));
        Assert.Equal("first-NamingMessage", first.GetEntityName(typeof(NamingMessage)));
        Assert.Equal("second-NamingMessage", second.GetEntityName(typeof(NamingMessage)));
        Assert.Equal(original, EntityNameFormatter.Format(typeof(NamingMessage)));
        first.Message<NamingMessage>(message => message.SetEntityName("explicit"));
        Assert.Equal("explicit", first.GetEntityName(typeof(NamingMessage)));
        Assert.Equal("second-NamingMessage", second.GetEntityName(typeof(NamingMessage)));
    }
}
