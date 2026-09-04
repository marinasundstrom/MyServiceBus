package com.myservicebus.topology;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.core.ConsumerInvoker;

class ConsumerRegistrationTest {
    @Test
    void snapshotsMessageContractsIndependentlyFromTheFrontendConsumerShape() {
        List<Class<?>> messageTypes = new ArrayList<>(List.of(TestMessage.class));

        ConsumerDefinitionModel definition = new ConsumerDefinitionModel(
                KotlinShapedConsumer.class,
                new EndpointDefinitionModel("test-message", true, null, 2, 5),
                messageTypes);
        messageTypes.clear();
        ConsumerInvoker<TestMessage> invoker = (provider, context) -> CompletableFuture.completedFuture(null);
        ConsumerRegistration<TestMessage> registration = new ConsumerRegistration<>(
                definition,
                TestMessage.class,
                invoker);

        assertEquals(List.of(TestMessage.class), definition.messageTypes());
        assertEquals(KotlinShapedConsumer.class, registration.definition().consumerType());
        assertEquals(TestMessage.class, registration.messageType());
        assertNotNull(registration.invoker());
    }

    @Test
    void rejectsAnInvocationForAMessageOutsideTheDefinition() {
        ConsumerDefinitionModel definition = new ConsumerDefinitionModel(
                KotlinShapedConsumer.class,
                new EndpointDefinitionModel("test-message", true, null, null, null),
                List.of(TestMessage.class));

        assertThrows(
                IllegalArgumentException.class,
                () -> new ConsumerRegistration<>(
                        definition,
                        OtherMessage.class,
                        (provider, context) -> CompletableFuture.completedFuture(null)));
    }

    private record TestMessage(String value) {
    }

    private record OtherMessage(String value) {
    }

    private static final class KotlinShapedConsumer {
        private KotlinShapedConsumer() {
        }
    }
}
