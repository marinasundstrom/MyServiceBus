package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

class PipeConfiguratorTest {
    static class TestContext implements PipeContext {
        private final CancellationToken token = CancellationToken.none;
        final List<String> calls = new ArrayList<>();
        @Override
        public CancellationToken getCancellationToken() {
            return token;
        }
    }

    static class Counter {
        int count;
    }

    static class DiFilter implements Filter<TestContext> {
        private final Counter counter;

        DiFilter(Counter counter) {
            this.counter = counter;
        }

        @Override
        public CompletableFuture<Void> send(TestContext context, Pipe<TestContext> next) {
            counter.count++;
            return next.send(context);
        }
    }

    @Test
    void executesFiltersInOrder() {
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        configurator.useExecute(ctx -> {
            ctx.calls.add("A");
            return CompletableFuture.completedFuture(null);
        });
        configurator.useExecute(ctx -> {
            ctx.calls.add("B");
            return CompletableFuture.completedFuture(null);
        });
        Pipe<TestContext> pipe = configurator.build();
        TestContext ctx = new TestContext();
        pipe.send(ctx).join();
        assertEquals(List.of("A", "B"), ctx.calls);
    }

    @Test
    void retryFilterRetriesOnFailure() {
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        AtomicInteger attempts = new AtomicInteger();
        configurator.useRetry(2);
        configurator.useExecute(ctx -> {
            if (attempts.incrementAndGet() < 3) {
                CompletableFuture<Void> failed = new CompletableFuture<>();
                failed.completeExceptionally(new RuntimeException("fail"));
                return failed;
            }
            ctx.calls.add("done");
            return CompletableFuture.completedFuture(null);
        });
        Pipe<TestContext> pipe = configurator.build();
        TestContext ctx = new TestContext();
        pipe.send(ctx).join();
        assertEquals(3, attempts.get());
        assertEquals(List.of("done"), ctx.calls);
    }

    @Test
    void messageRetryRetriesOnFailure() {
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        AtomicInteger attempts = new AtomicInteger();
        configurator.useMessageRetry(r -> r.immediate(2));
        configurator.useExecute(ctx -> {
            if (attempts.incrementAndGet() < 3) {
                CompletableFuture<Void> failed = new CompletableFuture<>();
                failed.completeExceptionally(new RuntimeException("fail"));
                return failed;
            }
            ctx.calls.add("done");
            return CompletableFuture.completedFuture(null);
        });
        Pipe<TestContext> pipe = configurator.build();
        TestContext ctx = new TestContext();
        pipe.send(ctx).join();
        assertEquals(3, attempts.get());
        assertEquals(List.of("done"), ctx.calls);
    }

    @Test
    void resolvesFilterFromServiceProvider() {
        ServiceCollection services = new ServiceCollection();
        services.addSingleton(Counter.class);
        services.addSingleton(DiFilter.class);
        ServiceProvider provider = services.buildServiceProvider();

        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        configurator.useFilter(DiFilter.class);
        Pipe<TestContext> pipe = configurator.build(provider);
        TestContext ctx = new TestContext();
        pipe.send(ctx).join();
        Counter counter = provider.getService(Counter.class);
        assertEquals(1, counter.count);
    }
}
