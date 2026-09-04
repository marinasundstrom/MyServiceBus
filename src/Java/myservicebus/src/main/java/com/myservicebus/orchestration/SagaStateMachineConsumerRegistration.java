package com.myservicebus.orchestration;

import com.myservicebus.BusHook;
import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.ConsumeContext;
import com.myservicebus.PublishEndpointProvider;
import com.myservicebus.SagaStateMachineHookEvent;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.di.ServiceProvider;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.function.Function;

/** Shared JVM adapter that registers one saga event as an ordinary bus consumer. */
public final class SagaStateMachineConsumerRegistration {
    private SagaStateMachineConsumerRegistration() {
    }

    public static <TSaga, TMessage> void register(
            BusRegistrationConfigurator configurator,
            Function<ServiceProvider, SagaStateMachineRuntime<TSaga>> runtimeFactory,
            Class<?> stateMachineClass,
            String endpointName,
            SagaStateMachineDefinition definition,
            String eventId,
            Class<TMessage> messageType,
            Function<TMessage, UUID> correlate) {
        Objects.requireNonNull(configurator, "configurator");
        Objects.requireNonNull(runtimeFactory, "runtimeFactory");
        Objects.requireNonNull(stateMachineClass, "stateMachineClass");
        Objects.requireNonNull(definition, "definition");
        Objects.requireNonNull(messageType, "messageType");
        Objects.requireNonNull(correlate, "correlate");
        required(endpointName, "endpointName");
        required(eventId, "eventId");

        configurator.addConsumerMethod(
                stateMachineClass,
                messageType,
                endpointName,
                true,
                null,
                (serviceProvider, context) -> {
                    SagaStateMachineRuntime<TSaga> runtime = runtimeFactory.apply(serviceProvider);
                    long startedAt = System.nanoTime();
                    return runtime.deliver(
                            context.getMessage(),
                            operation -> dispatchOutgoing(serviceProvider, context, operation))
                            .handle((result, failure) -> {
                                Throwable exception = unwrap(failure);
                                UUID failedCorrelationId = result == null
                                        ? tryCorrelate(correlate, context.getMessage())
                                        : null;
                                SagaStateMachineHookEvent hookEvent = new SagaStateMachineHookEvent(
                                        Instant.now(),
                                        exception == null,
                                        (System.nanoTime() - startedAt) / 1_000_000d,
                                        definition.stateMachineId(),
                                        definition.definitionVersion(),
                                        definition.owner(),
                                        eventId,
                                        result == null ? "faulted" : result.status().value(),
                                        result == null ? failedCorrelationId : result.correlationId(),
                                        result == null ? null : result.beginState(),
                                        result == null ? null : result.endState(),
                                        result != null && result.created(),
                                        result != null && result.completed(),
                                        result != null && result.instancePresent(),
                                        exception == null ? null : exception.getClass().getName(),
                                        exception == null ? null : exception.getMessage(),
                                        context.getMessageId() == null
                                                ? null
                                                : context.getMessageId().toString());
                                dispatchHooks(serviceProvider.getServices(BusHook.class), hookEvent);
                                if (exception != null) {
                                    throw new CompletionException(exception);
                                }
                                return (Void) null;
                            })
                            .toCompletableFuture();
                });
    }

    private static Throwable unwrap(Throwable failure) {
        if (failure instanceof CompletionException completion && completion.getCause() != null) {
            return completion.getCause();
        }
        return failure;
    }

    private static <TMessage> UUID tryCorrelate(
            Function<TMessage, UUID> correlate,
            TMessage message) {
        try {
            UUID correlationId = correlate.apply(message);
            return correlationId == null || correlationId.equals(new UUID(0, 0))
                    ? null
                    : correlationId;
        } catch (RuntimeException ignored) {
            return null;
        }
    }

    private static void dispatchHooks(Iterable<BusHook> hooks, SagaStateMachineHookEvent event) {
        for (BusHook hook : hooks) {
            try {
                hook.handle(event);
            } catch (RuntimeException ignored) {
                // Monitoring hooks cannot alter saga delivery outcomes.
            }
        }
    }

    private static CompletableFuture<Void> dispatchOutgoing(
            ServiceProvider serviceProvider,
            ConsumeContext<?> context,
            SagaStateMachineRuntime.OutgoingOperation operation) {
        return switch (operation.kind()) {
            case SEND -> serviceProvider.getRequiredService(SendEndpointProvider.class)
                    .getSendEndpoint(operation.destination())
                    .send(
                            operation.message(),
                            outgoing -> applyConsumeMetadata(outgoing, context),
                            context.getCancellationToken());
            case PUBLISH -> serviceProvider.getRequiredService(PublishEndpointProvider.class)
                    .getPublishEndpoint()
                    .publish(
                            operation.message(),
                            outgoing -> applyConsumeMetadata(outgoing, context),
                            context.getCancellationToken());
            default -> CompletableFuture.failedFuture(new IllegalStateException(
                    "Saga outgoing operation '" + operation.kind()
                            + "' cannot be dispatched through the bus."));
        };
    }

    private static void applyConsumeMetadata(
            com.myservicebus.SendContext outgoing,
            ConsumeContext<?> consumed) {
        outgoing.setRequestId(consumed.getRequestId());
        outgoing.setCorrelationId(consumed.getCorrelationId());
        outgoing.setConversationId(consumed.getConversationId());
        outgoing.setInitiatorId(consumed.getCorrelationId());
        outgoing.setCausationMessageId(consumed.getMessageId());
    }

    private static String required(String value, String name) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(name + " cannot be empty or whitespace.");
        }
        return value;
    }
}
