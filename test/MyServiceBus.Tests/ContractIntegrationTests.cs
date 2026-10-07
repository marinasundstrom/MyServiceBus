using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using System.Text;
using System.Text.Json;

namespace MyServiceBus.Tests;

public class ContractIntegrationTests
{
    [MessageUrn("urn:message:Acme:Order", false)]
    public record LocalOrder(int Quantity);
    public interface IOrder { int Quantity { get; } }
    [Fact]
    public void Identities_are_explicit_isolated_and_do_not_include_record_implementation_interfaces()
    {
        Assert.Equal("urn:message:Acme:Order", MessageUrn.For(typeof(LocalOrder)));
        Assert.Equal("urn:message:Acme:Order", new MessageUrnAttribute("Acme:Order").Urn);
        Assert.DoesNotContain(typeof(IEquatable<LocalOrder>), MessageTypeCache.GetMessageTypes(typeof(LocalOrder)));
        var first = new MessageContractRegistry();
        var second = new MessageContractRegistry();
        first.SetMessageUrn(typeof(LocalOrder), "urn:message:Acme:Alternate");
        first.Freeze([typeof(LocalOrder)]);
        Assert.NotEqual(first.GetMessageUrn(typeof(LocalOrder)), second.GetMessageUrn(typeof(LocalOrder)));
        Assert.Throws<InvalidOperationException>(() => first.SetMessageUrn(typeof(LocalOrder), "urn:message:Acme:Late"));
        second.SetMessageUrn(typeof(IOrder), MessageUrn.For(typeof(LocalOrder)));
        Assert.Throws<InvalidOperationException>(() => second.Freeze([typeof(LocalOrder), typeof(IOrder)]));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Startup_rejects_superseded_choreography_contracts(bool output)
    {
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddServiceBus(cfg =>
        {
            cfg.AddChoreography("orders", "1", "service", workflow => workflow
                .Step("submit", output ? "urn:external:Trigger" : MessageUrn.For(typeof(LocalOrder)), step =>
                    step.Publishes(output ? MessageUrn.For(typeof(LocalOrder)) : "urn:external:Output")));
            cfg.SetMessageUrn<LocalOrder>("urn:message:Acme:Replacement");
            cfg.UsingMediator();
        }));
        Assert.Contains("Choreography 'orders'", error.Message);
        Assert.Contains("urn:message:Acme:Replacement", error.Message);
    }

    [Fact]
    public void Registry_accepts_corrected_and_external_declarations()
    {
        var registry = new MessageContractRegistry();
        registry.SetMessageUrn(typeof(LocalOrder), "urn:message:Acme:Replacement");
        registry.Freeze([]);
        registry.ValidateDeclaration("urn:message:Acme:Replacement", "Saga 'orders'");
        registry.ValidateDeclaration("urn:external:Contract", "Saga 'orders'");
        Assert.Throws<InvalidOperationException>(() => registry.ValidateDeclaration(
            MessageUrn.For(typeof(LocalOrder)), "Saga 'orders'"));
    }
}
