package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Duration;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

class InMemoryHarnessObservationTest {
    record ObservedMessage() {
    }

    @Test
    void waitForConsumedCompletesAfterSuccessfulConsumerCompletion() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CompletableFuture<Void> release = new CompletableFuture<>();
        harness.registerHandler(ObservedMessage.class, context -> release);
        harness.start().join();

        CompletableFuture<Boolean> observation = harness.waitForConsumed(
                ObservedMessage.class, Duration.ofSeconds(1));
        CompletableFuture<Void> delivery = harness.send(new ObservedMessage());

        assertFalse(observation.isDone());
        release.complete(null);

        delivery.join();
        assertTrue(observation.join());
        assertTrue(harness.waitForConsumed(ObservedMessage.class, Duration.ZERO).join());
    }

    @Test
    void waitForConsumedReturnsFalseWhenTimeoutElapses() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        harness.start().join();

        assertFalse(harness.waitForConsumed(ObservedMessage.class, Duration.ofMillis(10)).join());
    }
}
