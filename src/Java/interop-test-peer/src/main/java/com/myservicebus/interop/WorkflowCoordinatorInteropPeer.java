package com.myservicebus.interop;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.MessageUrn;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.orchestration.SagaStateMachine;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

final class WorkflowCoordinatorInteropPeer {
    private WorkflowCoordinatorInteropPeer() {
    }

    static void run(String[] args) throws Exception {
        if (args.length != 5) {
            throw new IllegalArgumentException(
                    "Expected: workflow-coordinator <unused-exchange> <participant-queue> <order-id> <unused-durable>");
        }

        String host = requiredEnvironment("RABBITMQ_HOST");
        int port = Integer.parseInt(requiredEnvironment("RABBITMQ_PORT"));
        String username = requiredEnvironment("RABBITMQ_USERNAME");
        String password = requiredEnvironment("RABBITMQ_PASSWORD");
        UUID expectedOrderId = UUID.fromString(args[3]);
        CompletionObserver.reset();

        ServiceCollection services = ServiceCollection.create();
        services.from(MessageBusServices.class).addServiceBus(configurator -> {
            configurator.addSagaStateMachine(
                    JavaInteropWorkflowStateMachine.class,
                    () -> new JavaInteropWorkflowStateMachine(args[2]),
                    null);
            configurator.addConsumer(
                    CompletionObserver.class,
                    TestApp.InteropWorkflowCompleted.class,
                    null);
            configurator.using(RabbitMqFactoryConfigurator.class, (context, rabbit) -> {
                rabbit.host(host, port, settings -> {
                    settings.username(username);
                    settings.password(password);
                });
                rabbit.configureEndpoints(context);
            });
        });

        ServiceProvider provider = services.buildServiceProvider();
        MessageBus bus = provider.getRequiredService(MessageBus.class);
        try {
            bus.start();
            System.out.println("READY");
            System.out.flush();
            UUID completedOrderId = CompletionObserver.completed.get(30, TimeUnit.SECONDS);
            if (!expectedOrderId.equals(completedOrderId)) {
                throw new IllegalStateException(
                        "Expected workflow order '" + expectedOrderId + "' but completed '" + completedOrderId + "'");
            }
            System.out.println("COMPLETED");
            System.out.flush();
            System.exit(0);
        } finally {
            bus.stop();
        }
    }

    private static String requiredEnvironment(String name) {
        String value = System.getenv(name);
        if (value == null || value.isBlank()) {
            throw new IllegalStateException("Missing environment variable " + name);
        }
        return value;
    }

    public static final class JavaInteropWorkflowStateMachine extends SagaStateMachine<WorkflowState> {
        public JavaInteropWorkflowStateMachine(String participantQueue) {
            super(
                    "interop-order-workflow-java",
                    "1",
                    "Java.Saga",
                    MessageUrn.forClass(WorkflowState.class));
            instanceState(state -> state.currentState, (state, value) -> state.currentState = value);
            instanceFactory(WorkflowState::new);
            cloneInstance(WorkflowState::copy);

            State awaitingWork = state("AwaitingWork");
            Event<TestApp.InteropWorkflowStarted> started = event(
                    "WorkflowStarted",
                    TestApp.InteropWorkflowStarted.class,
                    correlation -> correlation
                            .correlateById(
                                    "CorrelationId",
                                    "OrderId",
                                    TestApp.InteropWorkflowStarted::getOrderId)
                            .createsIfMissing());
            Event<TestApp.InteropWorkflowWorkCompleted> workCompleted = event(
                    "WorkCompleted",
                    TestApp.InteropWorkflowWorkCompleted.class,
                    correlation -> correlation.correlateById(
                            "CorrelationId",
                            "OrderId",
                            TestApp.InteropWorkflowWorkCompleted::getOrderId));

            initially(when(started)
                    .send(
                            MessageUrn.forClass(TestApp.InteropWorkflowWorkRequested.class),
                            "queue:" + participantQueue,
                            context -> requested(context.message().getOrderId()))
                    .transitionTo(awaitingWork));
            during(awaitingWork, when(workCompleted)
                    .publish(
                            MessageUrn.forClass(TestApp.InteropWorkflowCompleted.class),
                            context -> completed(context.message().getOrderId()))
                    .finalizeSaga());
            deleteWhenFinalized();
        }

        private static TestApp.InteropWorkflowWorkRequested requested(UUID orderId) {
            TestApp.InteropWorkflowWorkRequested message = new TestApp.InteropWorkflowWorkRequested();
            message.setOrderId(orderId);
            return message;
        }

        private static TestApp.InteropWorkflowCompleted completed(UUID orderId) {
            TestApp.InteropWorkflowCompleted message = new TestApp.InteropWorkflowCompleted();
            message.setOrderId(orderId);
            return message;
        }
    }

    public static final class WorkflowState {
        private final UUID correlationId;
        private String currentState;

        public WorkflowState(UUID correlationId) {
            this.correlationId = correlationId;
        }

        private WorkflowState copy() {
            WorkflowState copy = new WorkflowState(correlationId);
            copy.currentState = currentState;
            return copy;
        }
    }

    public static final class CompletionObserver implements Consumer<TestApp.InteropWorkflowCompleted> {
        private static CompletableFuture<UUID> completed = new CompletableFuture<>();

        private static void reset() {
            completed = new CompletableFuture<>();
        }

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestApp.InteropWorkflowCompleted> context) {
            completed.complete(context.getMessage().getOrderId());
            return CompletableFuture.completedFuture(null);
        }
    }
}
