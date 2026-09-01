package com.myservicebus.persistence.postgresql;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportFactory;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.mediator.MediatorTransport;
import com.myservicebus.orchestration.SagaActivityKind;
import com.myservicebus.orchestration.SagaRepositoryTransaction;
import com.myservicebus.orchestration.SagaStateMachineDefinition;
import com.myservicebus.orchestration.SagaStateMachineDefinitionBuilder;
import com.myservicebus.orchestration.SagaStateMachineRuntime;
import com.myservicebus.orchestration.SagaStateMachineRuntime.OutgoingOperation;
import com.myservicebus.orchestration.SagaStateMachineRuntimeBuilder;
import com.myservicebus.persistence.OutboxSession;
import com.myservicebus.tasks.CancellationToken;
import java.net.URI;
import java.sql.Connection;
import java.sql.ResultSet;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.CompletionStage;
import javax.sql.DataSource;
import org.junit.jupiter.api.Test;
import org.postgresql.ds.PGSimpleDataSource;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.utility.DockerImageName;

class PostgreSqlSagaRecoveryTest {
    private static final String SERVICE_NAME = "saga-recovery-service";
    private static final String SAGA_TYPE = "tests.durable-order-saga";

    @Test
    void runtimeRecoversAfterFailureAndRestartSerializesConcurrentDeliveryAndDeletesFinalState()
            throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            ServiceCollection services = ServiceCollection.create();
            services.addSingleton(TransportFactory.class, ignored -> NoOpTransportFactory::new);
            services.from(MessageBusServices.class).addServiceBus(configurator -> {
                configurator.useBusOutbox();
                MediatorTransport.configure(configurator);
            });
            ServiceProvider provider = services.buildServiceProvider();
            MessageBus bus = provider.getRequiredService(MessageBus.class);
            bus.start();

            try {
                UUID failedOrderId = UUID.randomUUID();
                try (ServiceScope failedScope = provider.createScope()) {
                    ServiceProvider scoped = failedScope.getServiceProvider();
                    SagaStateMachineRuntime<DurableOrderState> failedRuntime = createRuntime(dataSource, scoped);
                    CompletionException failure = assertThrows(CompletionException.class, () ->
                            failedRuntime.deliver(
                                    new OrderSubmitted(failedOrderId),
                                    operation -> dispatch(scoped, operation).thenCompose(ignored ->
                                            CompletableFuture.failedFuture(new IllegalStateException(
                                                    "fail after staging outgoing work"))))
                                    .toCompletableFuture().join());
                    assertInstanceOf(IllegalStateException.class, failure.getCause());
                }

                assertNull(load(dataSource, failedOrderId));
                assertEquals(0, countOutboxMessages(dataSource));

                UUID orderId = UUID.randomUUID();
                try (ServiceScope firstProcess = provider.createScope()) {
                    ServiceProvider scoped = firstProcess.getServiceProvider();
                    SagaStateMachineRuntime.DeliveryResult submitted = createRuntime(dataSource, scoped)
                            .deliver(new OrderSubmitted(orderId), operation -> dispatch(scoped, operation))
                            .toCompletableFuture().join();
                    assertEquals("AwaitingPayment", submitted.endState());
                    assertEquals(true, submitted.created());
                }

                try (ServiceScope replicaA = provider.createScope();
                        ServiceScope replicaB = provider.createScope()) {
                    CompletableFuture<SagaStateMachineRuntime.DeliveryResult> deliveryA =
                            createRuntime(dataSource, replicaA.getServiceProvider())
                                    .deliver(new WorkObserved(orderId)).toCompletableFuture();
                    CompletableFuture<SagaStateMachineRuntime.DeliveryResult> deliveryB =
                            createRuntime(dataSource, replicaB.getServiceProvider())
                                    .deliver(new WorkObserved(orderId)).toCompletableFuture();
                    CompletableFuture.allOf(deliveryA, deliveryB).join();
                }

                DurableOrderState afterConcurrentDelivery = load(dataSource, orderId);
                assertEquals("AwaitingPayment", afterConcurrentDelivery.currentState);
                assertEquals(2, afterConcurrentDelivery.observedWork);

                try (ServiceScope secondProcess = provider.createScope()) {
                    SagaStateMachineRuntime.DeliveryResult payment = createRuntime(
                            dataSource, secondProcess.getServiceProvider())
                            .deliver(new PaymentReceived(orderId)).toCompletableFuture().join();
                    assertEquals("Processing", payment.endState());
                }

                try (ServiceScope finalProcess = provider.createScope()) {
                    ServiceProvider scoped = finalProcess.getServiceProvider();
                    SagaStateMachineRuntime.DeliveryResult completed = createRuntime(dataSource, scoped)
                            .deliver(new ProcessingCompleted(orderId), operation -> dispatch(scoped, operation))
                            .toCompletableFuture().join();
                    assertEquals(true, completed.completed());
                    assertFalse(completed.instancePresent());
                }

                assertNull(load(dataSource, orderId));
                assertEquals(2, countOutboxMessages(dataSource));
            } finally {
                bus.stop();
            }
        }
    }

    private static SagaStateMachineRuntime<DurableOrderState> createRuntime(
            DataSource dataSource,
            ServiceProvider services) {
        PostgreSqlSagaRepository<DurableOrderState> repository = new PostgreSqlSagaRepository<>(
                dataSource,
                services.getRequiredService(OutboxSession.class),
                SERVICE_NAME,
                SAGA_TYPE,
                DurableOrderState.class,
                new ObjectMapper().findAndRegisterModules());
        return new SagaStateMachineRuntimeBuilder<>(
                createDefinition(),
                repository,
                DurableOrderState::new,
                state -> state.currentState,
                (state, currentState) -> state.currentState = currentState)
                .event("OrderSubmitted", OrderSubmitted.class, OrderSubmitted::orderId)
                .event("WorkObserved", WorkObserved.class, WorkObserved::orderId)
                .event("PaymentReceived", PaymentReceived.class, PaymentReceived::orderId)
                .event("ProcessingCompleted", ProcessingCompleted.class, ProcessingCompleted::orderId)
                .mutate("Initial", "OrderSubmitted", 0, OrderSubmitted.class, context -> {
                    context.saga().orderId = context.message().orderId();
                    return CompletableFuture.completedFuture(null);
                })
                .message("Initial", "OrderSubmitted", 1, OrderSubmitted.class, context ->
                        CompletableFuture.completedFuture(new ReserveInventory(context.message().orderId())))
                .mutate("AwaitingPayment", "WorkObserved", 0, WorkObserved.class, context -> {
                    context.saga().observedWork++;
                    return CompletableFuture.completedFuture(null);
                })
                .mutate("AwaitingPayment", "PaymentReceived", 0, PaymentReceived.class, context -> {
                    context.saga().paymentReceived = true;
                    return CompletableFuture.completedFuture(null);
                })
                .message("Processing", "ProcessingCompleted", 0, ProcessingCompleted.class, context ->
                        CompletableFuture.completedFuture(new OrderCompleted(context.message().orderId())))
                .build();
    }

    private static SagaStateMachineDefinition createDefinition() {
        return new SagaStateMachineDefinitionBuilder(
                "durable-order-state-machine",
                "1",
                SERVICE_NAME,
                "urn:message:Tests:DurableOrderState",
                "CurrentState")
                .deleteWhenFinalized()
                .state("AwaitingPayment")
                .state("Processing")
                .event("OrderSubmitted", OrderSubmitted.class, event -> event
                        .correlateById("CorrelationId", "OrderId")
                        .createsIfMissing())
                .event("WorkObserved", WorkObserved.class, event -> event
                        .correlateById("CorrelationId", "OrderId"))
                .event("PaymentReceived", PaymentReceived.class, event -> event
                        .correlateById("CorrelationId", "OrderId"))
                .event("ProcessingCompleted", ProcessingCompleted.class, event -> event
                        .correlateById("CorrelationId", "OrderId"))
                .initially("OrderSubmitted", behavior -> behavior
                        .mutate("Initial.OrderSubmitted.0")
                        .send("urn:message:Tests:ReserveInventory", "loopback://reserve-inventory")
                        .transitionTo("AwaitingPayment"))
                .during("AwaitingPayment", "WorkObserved", behavior -> behavior
                        .mutate("AwaitingPayment.WorkObserved.0"))
                .during("AwaitingPayment", "PaymentReceived", behavior -> behavior
                        .mutate("AwaitingPayment.PaymentReceived.0")
                        .transitionTo("Processing"))
                .during("Processing", "ProcessingCompleted", behavior -> behavior
                        .publish("urn:message:Tests:OrderCompleted")
                        .finalizeSaga())
                .build();
    }

    private static CompletionStage<Void> dispatch(ServiceProvider services, OutgoingOperation operation) {
        if (operation.kind() == SagaActivityKind.SEND) {
            return services.getRequiredService(SendEndpointProvider.class)
                    .getSendEndpoint(operation.destination())
                    .send(operation.message(), CancellationToken.none());
        }
        if (operation.kind() == SagaActivityKind.PUBLISH) {
            return services.getRequiredService(PublishEndpoint.class)
                    .publish(operation.message(), CancellationToken.none());
        }
        return CompletableFuture.failedFuture(
                new IllegalStateException("Unexpected saga output kind " + operation.kind()));
    }

    private static DurableOrderState load(DataSource dataSource, UUID correlationId) {
        PostgreSqlSagaRepository<DurableOrderState> repository = new PostgreSqlSagaRepository<>(
                dataSource,
                new OutboxSession(),
                SERVICE_NAME,
                SAGA_TYPE,
                DurableOrderState.class,
                new ObjectMapper().findAndRegisterModules());
        return repository.execute(correlationId, instance -> CompletableFuture.completedFuture(
                SagaRepositoryTransaction.noChange(instance))).toCompletableFuture().join();
    }

    private static long countOutboxMessages(DataSource dataSource) throws Exception {
        try (Connection connection = dataSource.getConnection();
                var statement = connection.prepareStatement(
                        "SELECT count(*) FROM myservicebus.outbox_message WHERE service_name = ?")) {
            statement.setString(1, SERVICE_NAME);
            try (ResultSet result = statement.executeQuery()) {
                result.next();
                return result.getLong(1);
            }
        }
    }

    private static PostgreSQLContainer startContainer() {
        PostgreSQLContainer container = new PostgreSQLContainer(
                DockerImageName.parse("postgres:17.6-alpine"));
        container.start();
        return container;
    }

    private static DataSource dataSource(PostgreSQLContainer container) {
        PGSimpleDataSource dataSource = new PGSimpleDataSource();
        dataSource.setURL(container.getJdbcUrl());
        dataSource.setUser(container.getUsername());
        dataSource.setPassword(container.getPassword());
        return dataSource;
    }

    public static final class DurableOrderState {
        public UUID correlationId;
        public UUID orderId;
        public String currentState;
        public boolean paymentReceived;
        public int observedWork;

        public DurableOrderState() {
        }

        public DurableOrderState(UUID correlationId) {
            this.correlationId = correlationId;
        }
    }

    private record OrderSubmitted(UUID orderId) {
    }

    private record WorkObserved(UUID orderId) {
    }

    private record PaymentReceived(UUID orderId) {
    }

    private record ProcessingCompleted(UUID orderId) {
    }

    private record ReserveInventory(UUID orderId) {
    }

    private record OrderCompleted(UUID orderId) {
    }

    private static final class NoOpTransportFactory implements TransportFactory {
        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> {
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "loopback://" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "loopback://" + queue;
        }
    }
}
