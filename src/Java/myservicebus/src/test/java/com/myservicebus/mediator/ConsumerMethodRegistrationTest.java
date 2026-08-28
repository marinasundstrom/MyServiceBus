package com.myservicebus.mediator;

import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.ConsumerMethodInvoker;
import com.myservicebus.MessageConsumer;
import com.myservicebus.SendEndpoint;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.tasks.CancellationToken;

public class ConsumerMethodRegistrationTest {
    public record Order(String number) {
    }

    public record ClassMappedOrder(String number) {
    }

    public record MethodMappedOrder(String number) {
    }

    public record ConventionMappedOrder(String number) {
    }

    public record ResponseRequest(String value) {
    }

    public record ResponseMessage(String value) {
    }

    public static final class Audit {
        private Order message;
        private ConsumeContext<Order> context;
        private CancellationToken cancellationToken;
    }

    public static final class MethodOnlyConsumers {
        private MethodOnlyConsumers() {
        }

        @MessageConsumer("method-orders")
        public static CompletionStage<Void> receiveOrder(
                Order order,
                ConsumeContext<Order> context,
                Audit audit,
                CancellationToken cancellationToken) {
            audit.message = order;
            audit.context = context;
            audit.cancellationToken = cancellationToken;
            return CompletableFuture.completedFuture(null);
        }
    }

    @MessageConsumer("class-orders")
    public static final class GroupedConsumers {
        private GroupedConsumers() {
        }

        public static void observeClass(ClassMappedOrder order) {
        }

        @MessageConsumer("method-orders")
        public static void observeMethod(MethodMappedOrder order) {
        }
    }

    public static final class ConventionConsumers {
        private ConventionConsumers() {
        }

        @MessageConsumer
        public static void orderSubmittedConsumer(ConventionMappedOrder order) {
        }
    }

    public static final class ResponseConsumers {
        private ResponseConsumers() {
        }

        public static CompletableFuture<ResponseMessage> respond(ResponseRequest request) {
            return CompletableFuture.completedFuture(new ResponseMessage(request.value() + "-response"));
        }
    }

    @MessageConsumer("interface-orders")
    public static final class InterfaceConsumer implements Consumer<Order> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<Order> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    public void methodAttributeOnUnannotatedContainerBindsMessageContextServiceAndCancellation() {
        ServiceCollection services = ServiceCollection.create();
        Audit audit = new Audit();
        services.addSingleton(Audit.class, ignored -> () -> audit);
        MediatorBus bus = MediatorBus.configure(services, configurator ->
                configurator.addConsumerMethods(MethodOnlyConsumers.class));

        Order order = new Order("A-42");
        bus.publish(order);

        Assertions.assertSame(order, audit.message);
        Assertions.assertSame(order, audit.context.getMessage());
        Assertions.assertSame(audit.context.getCancellationToken(), audit.cancellationToken);
    }

    @Test
    public void messageConsumerOnInterfaceConsumerOverridesEndpointMapping() {
        ServiceCollection services = ServiceCollection.create();
        var configurator = new BusRegistrationConfiguratorImpl(services);
        configurator.addConsumer(InterfaceConsumer.class);
        configurator.complete();
        var topology = services.buildServiceProvider()
                .getRequiredService(com.myservicebus.topology.TopologyRegistry.class);

        Assertions.assertEquals("interface-orders", topology.getConsumers().get(0).getQueueName());
        Assertions.assertNull(topology.getConsumers().get(0).getMethodInvoker());
    }

    @Test
    public void explicitInvokerUsesTheSameMethodConsumerTopology() {
        ServiceCollection services = ServiceCollection.create();
        Audit audit = new Audit();
        services.addSingleton(Audit.class, ignored -> () -> audit);
        MediatorBus bus = MediatorBus.configure(services, configurator ->
                configurator.addConsumerMethod(
                        MethodOnlyConsumers.class,
                        Order.class,
                        "generated-orders",
                        (provider, context) -> {
                            provider.getRequiredService(Audit.class).message = context.getMessage();
                            return CompletableFuture.completedFuture(null);
                        }));

        Order order = new Order("G-42");
        bus.publish(order);

        Assertions.assertSame(order, audit.message);
    }

    @Test
    @SuppressWarnings("unchecked")
    public void reflectionResponseMethodRespondsWithCompletedValue() throws Exception {
        ServiceCollection services = ServiceCollection.create();
        var configurator = new BusRegistrationConfiguratorImpl(services);
        configurator.addConsumerMethods(ResponseConsumers.class);
        configurator.complete();
        var provider = services.buildServiceProvider();
        var topology = provider.getRequiredService(com.myservicebus.topology.TopologyRegistry.class);
        var consumer = topology.getConsumers().get(0);
        ConsumerMethodInvoker<ResponseRequest> invoker =
                (ConsumerMethodInvoker<ResponseRequest>) consumer.getMethodInvoker();
        CapturingSendEndpoint endpoint = new CapturingSendEndpoint();
        ConsumeContext<ResponseRequest> context = new ConsumeContext<>(
                new ResponseRequest("reflection"),
                Map.of(),
                "queue:response",
                null,
                CancellationToken.none(),
                ignored -> endpoint);

        try (var scope = provider.createScope()) {
            invoker.invoke(scope.getServiceProvider(), context).join();
        }

        ResponseMessage response = (ResponseMessage) endpoint.sent.join();
        Assertions.assertEquals("reflection-response", response.value());
    }

    @Test
    public void endpointPrecedenceIsFluentThenMethodThenClassThenConvention() {
        ServiceCollection services = ServiceCollection.create();
        var configurator = new BusRegistrationConfiguratorImpl(services);
        configurator.addConsumerMethods(GroupedConsumers.class);
        configurator.addConsumerMethods(ConventionConsumers.class);
        configurator.complete();

        var topology = services.buildServiceProvider()
                .getRequiredService(com.myservicebus.topology.TopologyRegistry.class);
        Assertions.assertEquals("class-orders", endpointFor(topology, ClassMappedOrder.class));
        Assertions.assertEquals("method-orders", endpointFor(topology, MethodMappedOrder.class));
        Assertions.assertEquals("orderSubmittedConsumer", endpointFor(topology, ConventionMappedOrder.class));
    }

    @Test
    public void fluentEndpointOverridesClassAndMethodAttributes() {
        ServiceCollection services = ServiceCollection.create();
        var configurator = new BusRegistrationConfiguratorImpl(services);
        configurator.addConsumerMethods(GroupedConsumers.class, "fluent-orders");
        configurator.complete();

        var topology = services.buildServiceProvider()
                .getRequiredService(com.myservicebus.topology.TopologyRegistry.class);
        Assertions.assertEquals(2, topology.getConsumers().size());
        Assertions.assertTrue(topology.getConsumers().stream()
                .allMatch(consumer -> consumer.getQueueName().equals("fluent-orders")));
        Assertions.assertEquals(1, topology.getReceiveEndpoints().size());
    }

    private static String endpointFor(
            com.myservicebus.topology.TopologyRegistry topology,
            Class<?> messageType) {
        return topology.getConsumers().stream()
                .filter(consumer -> consumer.getBindings().get(0).getMessageType().equals(messageType))
                .findFirst()
                .orElseThrow()
                .getQueueName();
    }

    private static final class CapturingSendEndpoint implements SendEndpoint {
        private final CompletableFuture<Object> sent = new CompletableFuture<>();

        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            sent.complete(message);
            return CompletableFuture.completedFuture(null);
        }
    }
}
