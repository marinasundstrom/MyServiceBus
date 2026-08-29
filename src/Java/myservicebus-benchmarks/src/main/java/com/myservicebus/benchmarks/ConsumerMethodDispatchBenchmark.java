package com.myservicebus.benchmarks;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Level;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Warmup;

import com.myservicebus.ConsumeContext;
import com.myservicebus.MessageConsumer;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.generated.GeneratedConsumerCatalog;
import com.myservicebus.mediator.MediatorBus;

@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.MICROSECONDS)
@Warmup(iterations = 5, time = 500, timeUnit = TimeUnit.MILLISECONDS)
@Measurement(iterations = 10, time = 500, timeUnit = TimeUnit.MILLISECONDS)
@Fork(2)
@State(Scope.Benchmark)
public class ConsumerMethodDispatchBenchmark {
    public record MethodMessage(String value) {
    }

    public static final class MethodConsumers {
        private MethodConsumers() {
        }

        @MessageConsumer("benchmark-method")
        public static CompletableFuture<Void> consume(MethodMessage message) {
            return CompletableFuture.completedFuture(null);
        }
    }

    private MediatorBus reflectionBus;
    private MediatorBus generatedBus;
    private MethodMessage message;

    @Setup(Level.Trial)
    public void setup() {
        reflectionBus = MediatorBus.configure(
                ServiceCollection.create(),
                configurator -> configurator.addConsumerMethods(MethodConsumers.class));
        generatedBus = MediatorBus.configure(
                ServiceCollection.create(),
                GeneratedConsumerCatalog.INSTANCE::register);
        message = new MethodMessage("benchmark");
    }

    @Benchmark
    public void reflectionInvocation() {
        reflectionBus.publish(message).join();
    }

    @Benchmark
    public void generatedDirectInvocation() {
        generatedBus.publish(message).join();
    }
}
