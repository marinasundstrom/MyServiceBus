package com.myservicebus.packagesmoke;

import java.util.concurrent.atomic.AtomicBoolean;

import com.myservicebus.InMemoryTestHarness;
import com.myservicebus.MessageBus;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;

public final class PackageSmoke {
    private PackageSmoke() {
    }

    public static void main(String[] args) {
        AtomicBoolean consumed = new AtomicBoolean();
        InMemoryTestHarness harness = new InMemoryTestHarness();
        harness.registerHandler(SmokeMessage.class, context -> {
            consumed.set(true);
            return java.util.concurrent.CompletableFuture.completedFuture(null);
        });

        harness.start().join();
        harness.publish(new SmokeMessage("package-smoke")).join();
        harness.stop().join();

        if (!consumed.get()) {
            throw new IllegalStateException("The packaged in-memory harness did not deliver the message.");
        }

        requireType(MessageBus.class);
        requireType(RabbitMqFactoryConfigurator.class);
        System.out.println("Verified the staged MyServiceBus Maven packages from a consumer project.");
    }

    private static void requireType(Class<?> type) {
        if (type == null) {
            throw new AssertionError("Expected a packaged API type.");
        }
    }

    private record SmokeMessage(String value) {
    }
}
