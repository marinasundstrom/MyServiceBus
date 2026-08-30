using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    internal sealed class UnionAttribute : Attribute;

    internal interface IUnion
    {
        object Value { get; }
    }
}
#endif

namespace MyServiceBus.Tests
{
    public class ReflectionUnionConsumerMethodTests
    {
        [Fact]
        public async Task Abi_union_consumer_expands_concrete_contracts_and_constructs_the_selected_case()
        {
            var services = new ServiceCollection();
            services.AddSingleton<UnionConsumerAudit>();
            services.AddServiceBus(configurator =>
            {
                configurator.UsingMediator();
                configurator.AddConsumerMethods(typeof(UnionConsumers));
            });

            await using var provider = services.BuildServiceProvider();
            var topology = provider.GetRequiredService<Topology.TopologyRegistry>();
            Assert.Equal(2, topology.Consumers.Count);
            Assert.Single(topology.ReceiveEndpoints);
            Assert.Contains(topology.Messages, message => message.MessageType == typeof(FirstUnionMessage));
            Assert.Contains(topology.Messages, message => message.MessageType == typeof(SecondUnionMessage));
            Assert.DoesNotContain(topology.Messages, message => message.MessageType == typeof(CompatibilityUnion<FirstUnionMessage, SecondUnionMessage>));

            var hostedService = provider.GetRequiredService<IHostedService>();
            await hostedService.StartAsync(CancellationToken.None);
            try
            {
                var mediator = provider.GetRequiredService<IMediator>();
                await mediator.Send(new FirstUnionMessage("first"));
                await mediator.Send(new SecondUnionMessage("second"));

                var audit = provider.GetRequiredService<UnionConsumerAudit>();
                Assert.Equal(["first:first", "second:second"], audit.Events);
            }
            finally
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
        }

        private sealed record FirstUnionMessage(string Value);

        private sealed record SecondUnionMessage(string Value);

        [System.Runtime.CompilerServices.Union]
        private readonly struct CompatibilityUnion<T1, T2> : System.Runtime.CompilerServices.IUnion
        {
            public CompatibilityUnion(T1 value)
            {
                Value = value!;
            }

            public CompatibilityUnion(T2 value)
            {
                Value = value!;
            }

            public object Value { get; }
        }

        private sealed class UnionConsumerAudit
        {
            public List<string> Events { get; } = [];
        }

        private static class UnionConsumers
        {
            [Consumer("union-messages")]
            public static void Consume(
                CompatibilityUnion<FirstUnionMessage, SecondUnionMessage> message,
                UnionConsumerAudit audit)
            {
                switch (message.Value)
                {
                    case FirstUnionMessage first:
                        audit.Events.Add($"first:{first.Value}");
                        break;
                    case SecondUnionMessage second:
                        audit.Events.Add($"second:{second.Value}");
                        break;
                }
            }
        }
    }
}
