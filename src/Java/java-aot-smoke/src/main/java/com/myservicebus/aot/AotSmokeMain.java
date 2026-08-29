package com.myservicebus.aot;

import java.util.Arrays;
import java.util.concurrent.CompletableFuture;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.generated.GeneratedConsumerCatalog;
import com.myservicebus.mediator.MediatorBus;
import com.myservicebus.serialization.EnvelopeSerializerFactory;
import com.myservicebus.tasks.CancellationToken;

public final class AotSmokeMain {
    private AotSmokeMain() {
    }

    public record SmokeMessage(String value) {
    }

    public static final class Probe {
        private SmokeMessage message;
        private ConsumeContext<SmokeMessage> context;
        private CancellationToken cancellationToken;

        void record(
                SmokeMessage message,
                ConsumeContext<SmokeMessage> context,
                CancellationToken cancellationToken) {
            this.message = message;
            this.context = context;
            this.cancellationToken = cancellationToken;
        }
    }

    public static final class SmokeConsumer implements Consumer<SmokeMessage> {
        private final Probe probe;

        public SmokeConsumer(Probe probe) {
            this.probe = probe;
        }

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<SmokeMessage> context) {
            probe.record(context.getMessage(), context, context.getCancellationToken());
            return CompletableFuture.completedFuture(null);
        }
    }

    public static void main(String[] args) {
        ServiceCollection services = ServiceCollection.createAot();
        Probe probe = new Probe();
        services.addSingleton(Probe.class, () -> probe);
        MediatorBus bus = MediatorBus.configure(services, configurator -> {
            GeneratedConsumerCatalog.INSTANCE.register(configurator);
            services.remove(SmokeConsumer.class);
            services.addScoped(SmokeConsumer.class, ignored -> () -> new SmokeConsumer(probe));
            EnvelopeSerializerFactory serialization = new EnvelopeSerializerFactory();
            configurator.addSerializer(serialization, true);
            configurator.addDeserializer(serialization, true);
        });

        SmokeMessage message = new SmokeMessage("native-ready");

        if (args.length > 0 && args[0].equals("--benchmark")) {
            runBenchmark(bus, message);
            return;
        }

        bus.publish(message).join();

        if (probe.message != message
                || probe.context.getMessage() != message
                || probe.context.getCancellationToken() != probe.cancellationToken) {
            throw new IllegalStateException("Generated consumer dispatch did not preserve its bound values");
        }

        System.out.println("Generated interface-consumer dispatch AOT smoke test passed");
    }

    private static void runBenchmark(MediatorBus bus, SmokeMessage message) {
        int warmupOperations = 20_000;
        int operationsPerSample = 100_000;
        int sampleCount = 10;

        for (int index = 0; index < warmupOperations; index++) {
            bus.publish(message).join();
        }

        double[] throughput = new double[sampleCount];
        for (int sample = 0; sample < sampleCount; sample++) {
            long started = System.nanoTime();
            for (int index = 0; index < operationsPerSample; index++) {
                bus.publish(message).join();
            }
            long elapsed = System.nanoTime() - started;
            throughput[sample] = operationsPerSample * 1_000_000_000.0 / elapsed;
        }

        Arrays.sort(throughput);
        double median = (throughput[4] + throughput[5]) / 2.0;
        System.out.printf(
                "Generated mediator dispatch throughput: %.0f ops/s median (%d samples x %d operations)%n",
                median,
                sampleCount,
                operationsPerSample);
    }
}
