package com.myservicebus.benchmarks;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Warmup;

import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.generated.GeneratedConsumerCatalog;

@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.MICROSECONDS)
@Warmup(iterations = 5, time = 500, timeUnit = TimeUnit.MILLISECONDS)
@Measurement(iterations = 10, time = 500, timeUnit = TimeUnit.MILLISECONDS)
@Fork(2)
@State(Scope.Benchmark)
public class ConsumerRegistrationBenchmark {
    public record BenchmarkMessage(String value) {
    }

    public static final class BenchmarkConsumer implements Consumer<BenchmarkMessage> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<BenchmarkMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @Benchmark
    public ServiceCollection reflectionSingleType() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        configurator.addConsumer(BenchmarkConsumer.class);
        configurator.complete();
        return services;
    }

    @Benchmark
    public ServiceCollection explicitTyped() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        configurator.addConsumer(BenchmarkConsumer.class, BenchmarkMessage.class, null);
        configurator.complete();
        return services;
    }

    @Benchmark
    public ServiceCollection reflectionCatalog() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        configurator.addConsumer(BenchmarkConsumer.class);
        configurator.addConsumerMethods(ConsumerMethodDispatchBenchmark.MethodConsumers.class);
        configurator.complete();
        return services;
    }

    @Benchmark
    public ServiceCollection generatedCatalog() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        GeneratedConsumerCatalog.INSTANCE.register(configurator);
        configurator.complete();
        return services;
    }
}
