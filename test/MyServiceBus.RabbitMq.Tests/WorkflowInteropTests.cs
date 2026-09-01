using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MassTransit;
using MyServiceBus.Choreography;
using MyServiceBus.Orchestration;
using Testcontainers.RabbitMq;

namespace MyServiceBus.RabbitMq.Tests
{
    [Collection(RabbitMqInteroperabilityCollection.Name)]
    public class WorkflowInteropTests
    {
        [CrossLanguageFact]
        public async Task MassTransit_and_Java_participate_in_a_CSharp_saga_workflow()
        {
            await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
            await container.StartAsync();

            var orderId = Guid.NewGuid();
            var javaQueue = $"workflow-java-{Guid.NewGuid():N}";
            var requestExchange = EntityNameFormatter.Format(typeof(TestApp.InteropWorkflowWorkRequested));
            using var javaPeer = JavaInteropPeer.Start(
                container,
                "workflow-participant",
                requestExchange,
                javaQueue,
                orderId.ToString());
            await JavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

            var connectionUri = new Uri(container.GetConnectionString());
            var credentials = connectionUri.UserInfo.Split(':', 2);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceBus(configurator =>
            {
                configurator.AddSagaStateMachine<InteropWorkflowStateMachine, InteropWorkflowState>(
                    new InteropWorkflowStateMachine(javaQueue));
                configurator.AddChoreography(
                    "interop-order-workflow",
                    "1",
                    "CSharp.Saga",
                    workflow => workflow
                        .Step<TestApp.InteropWorkflowStarted>("coordinate-work", step => step
                            .OwnedBy<InteropWorkflowStateMachine>()
                            .Sends<TestApp.InteropWorkflowWorkRequested>($"queue:{javaQueue}"))
                        .Step<TestApp.InteropWorkflowWorkCompleted>("complete-work", step => step
                            .OwnedBy<InteropWorkflowStateMachine>()
                            .Publishes<TestApp.InteropWorkflowCompleted>()));
                configurator.UsingRabbitMq((_, rabbit) => rabbit.Host(
                    container.Hostname,
                    container.GetMappedPublicPort(5672),
                    host =>
                    {
                        host.Username(Uri.UnescapeDataString(credentials[0]));
                        host.Password(Uri.UnescapeDataString(credentials[1]));
                    }));
            });

            await using var provider = services.BuildServiceProvider();
            var hostedService = provider.GetServices<IHostedService>().OfType<ServiceBusHostedService>().Single();
            var completed = new TaskCompletionSource<TestApp.InteropWorkflowCompleted>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var massTransitBus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
            {
                configurator.Host(connectionUri);
                configurator.ReceiveEndpoint($"workflow-masstransit-{Guid.NewGuid():N}", endpoint =>
                    endpoint.Consumer(() => new WorkflowCompletedConsumer(completed)));
            });

            await hostedService.StartAsync(CancellationToken.None);
            await massTransitBus.StartAsync();
            try
            {
                await massTransitBus.Publish(new TestApp.InteropWorkflowStarted { OrderId = orderId });

                var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await JavaInteropPeer.WaitForOutput(javaPeer, "COMPLETED", TimeSpan.FromSeconds(20));
                await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

                Assert.Equal(orderId, result.OrderId);
                Assert.Equal(
                    0,
                    provider.GetRequiredService<MyServiceBus.Orchestration.InMemorySagaRepository<InteropWorkflowState>>().Count);
                Assert.Equal(0, javaPeer.ExitCode);
            }
            finally
            {
                if (!javaPeer.HasExited)
                    javaPeer.Kill(entireProcessTree: true);
                await massTransitBus.StopAsync();
                await hostedService.StopAsync(CancellationToken.None);
            }
        }

        private sealed class WorkflowCompletedConsumer : MassTransit.IConsumer<TestApp.InteropWorkflowCompleted>
        {
            private readonly TaskCompletionSource<TestApp.InteropWorkflowCompleted> completed;

            public WorkflowCompletedConsumer(TaskCompletionSource<TestApp.InteropWorkflowCompleted> completed)
            {
                this.completed = completed;
            }

            public Task Consume(MassTransit.ConsumeContext<TestApp.InteropWorkflowCompleted> context)
            {
                completed.TrySetResult(context.Message);
                return Task.CompletedTask;
            }
        }

        public sealed class InteropWorkflowStateMachine : MyServiceBus.Orchestration.SagaStateMachine<InteropWorkflowState>
        {
            public InteropWorkflowStateMachine(string javaQueue)
                : base("interop-order-workflow", "1", "CSharp.Saga")
            {
                InstanceState(state => state.CurrentState, (state, value) => state.CurrentState = value);
                InstanceFactory(id => new InteropWorkflowState { CorrelationId = id });
                CloneInstance(state => state.Copy());

                var awaitingWork = State("AwaitingWork");
                var started = Event<TestApp.InteropWorkflowStarted>("WorkflowStarted", correlation => correlation
                    .CorrelateById("CorrelationId", "OrderId", message => message.OrderId)
                    .CreatesIfMissing());
                var workCompleted = Event<TestApp.InteropWorkflowWorkCompleted>("WorkCompleted", correlation => correlation
                    .CorrelateById("CorrelationId", "OrderId", message => message.OrderId));

                Initially(When(started)
                    .Send(
                        MessageUrn.For(typeof(TestApp.InteropWorkflowWorkRequested)),
                        $"queue:{javaQueue}",
                        context => new TestApp.InteropWorkflowWorkRequested { OrderId = context.Message.OrderId })
                    .TransitionTo(awaitingWork));
                During(awaitingWork, When(workCompleted)
                    .Publish(
                        MessageUrn.For(typeof(TestApp.InteropWorkflowCompleted)),
                        context => new TestApp.InteropWorkflowCompleted { OrderId = context.Message.OrderId })
                    .Finalize());
                DeleteWhenFinalized();
            }
        }

        public sealed class InteropWorkflowState
        {
            public Guid CorrelationId { get; init; }
            public string? CurrentState { get; set; }

            public InteropWorkflowState Copy() => new()
            {
                CorrelationId = CorrelationId,
                CurrentState = CurrentState
            };
        }
    }
}

namespace TestApp
{
    public sealed class InteropWorkflowStarted
    {
        public Guid OrderId { get; set; }
    }

    public sealed class InteropWorkflowWorkRequested
    {
        public Guid OrderId { get; set; }
    }

    public sealed class InteropWorkflowWorkCompleted
    {
        public Guid OrderId { get; set; }
    }

    public sealed class InteropWorkflowCompleted
    {
        public Guid OrderId { get; set; }
    }
}
