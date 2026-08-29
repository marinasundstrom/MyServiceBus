package com.myservicebus.packagesmoke;

import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;

import com.myservicebus.InMemoryTestHarness;
import com.myservicebus.MessageConsumer;
import com.myservicebus.MessageBus;
import com.myservicebus.azure.servicebus.AzureServiceBusFactoryConfigurator;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.generated.GeneratedConsumerCatalog;
import com.myservicebus.inspection.BusInspectionProvider;
import com.myservicebus.mediator.MediatorBus;
import com.myservicebus.monitoring.MonitoringExporterOptions;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import com.myservicebus.serialization.bson.BsonSerializerFactory;

public final class PackageSmoke {
    private static final AtomicBoolean generatedConsumerInvoked = new AtomicBoolean();

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

        MediatorBus mediator = MediatorBus.configure(
                ServiceCollection.create(),
                GeneratedConsumerCatalog.INSTANCE::register);
        mediator.publish(new GeneratedSmokeMessage("generated-package-smoke"));
        if (!generatedConsumerInvoked.get()) {
            throw new IllegalStateException("The packaged annotation processor did not generate consumer dispatch.");
        }

        requireType(MessageBus.class);
        requireType(BusInspectionProvider.class);
        requireType(MonitoringExporterOptions.class);
        requireType(RabbitMqFactoryConfigurator.class);
        requireType(AzureServiceBusFactoryConfigurator.class);
        requireType(BsonSerializerFactory.class);
        System.out.println("Verified the staged MyServiceBus Maven packages from a consumer project.");
    }

    private static void requireType(Class<?> type) {
        if (type == null) {
            throw new AssertionError("Expected a packaged API type.");
        }
    }

    @MessageConsumer
    public static CompletionStage<Void> consumeGenerated(GeneratedSmokeMessage message) {
        generatedConsumerInvoked.set(message.value().equals("generated-package-smoke"));
        return CompletableFuture.completedFuture(null);
    }

    private record SmokeMessage(String value) {
    }

    public record GeneratedSmokeMessage(String value) {
    }
}
