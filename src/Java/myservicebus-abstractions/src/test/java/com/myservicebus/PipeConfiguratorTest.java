package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;

import org.junit.jupiter.api.Test;
import javax.inject.Inject;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.tasks.CancellationTokenSource;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

class PipeConfiguratorTest {
    static class TestContext implements PipeContext {
        private final CancellationToken token;
        final List<String> calls = new ArrayList<>();

        TestContext() {
            this(CancellationToken.none);
        }

        TestContext(CancellationToken token) {
            this.token = token;
        }

        @Override
        public CancellationToken getCancellationToken() {
            return token;
        }
    }

    static class RecordingFilter implements Filter<TestContext> {
        private final String name;

        RecordingFilter(String name) {
            this.name = name;
        }

        @Override
        public CompletableFuture<Void> send(TestContext context, Pipe<TestContext> next) {
            context.calls.add(name + ":before");
            return next.send(context).thenRun(() -> context.calls.add(name + ":after"));
        }
    }

    static class Counter {
        int count;
    }

    static class DiFilter implements Filter<TestContext> {
        private final Counter counter;

        @Inject
        DiFilter(Counter counter) {
            this.counter = counter;
        }

        @Override
        public CompletableFuture<Void> send(TestContext context, Pipe<TestContext> next) {
            counter.count++;
            return next.send(context);
        }
    }

    static class ScopedFilterState {
        final CompletableFuture<Void> completion = new CompletableFuture<>();
        boolean disposed;
    }

    static class ScopedTestFilter implements Filter<TestContext>, AutoCloseable {
        private final ScopedFilterState state;

        @Inject
        ScopedTestFilter(ScopedFilterState state) {
            this.state = state;
        }

        @Override
        public CompletableFuture<Void> send(TestContext context, Pipe<TestContext> next) {
            return state.completion.thenCompose(ignored -> next.send(context));
        }

        @Override
        public void close() {
            state.disposed = true;
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
    void filtersWrapDownstreamInRegistrationOrder() {
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        configurator.useFilter(new RecordingFilter("outer"));
        configurator.useFilter(new RecordingFilter("inner"));

        TestContext ctx = new TestContext();
        configurator.build().send(ctx).join();

        assertEquals(List.of("outer:before", "inner:before", "inner:after", "outer:after"), ctx.calls);
    }

    @Test
    void filterCanShortCircuitDownstreamPipeline() {
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        configurator.useFilter((context, next) -> {
            context.calls.add("stopped");
            return CompletableFuture.completedFuture(null);
        });
        configurator.useExecute(context -> {
            context.calls.add("downstream");
            return CompletableFuture.completedFuture(null);
        });

        TestContext ctx = new TestContext();
        configurator.build().send(ctx).join();

        assertEquals(List.of("stopped"), ctx.calls);
    }

    @Test
    void exceptionStopsPipelineAndPropagatesUnchanged() {
        RuntimeException expected = new RuntimeException("failed");
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        configurator.useExecute(context -> CompletableFuture.failedFuture(expected));
        configurator.useExecute(context -> {
            context.calls.add("downstream");
            return CompletableFuture.completedFuture(null);
        });

        TestContext ctx = new TestContext();
        java.util.concurrent.CompletionException actual = assertThrows(
                java.util.concurrent.CompletionException.class,
                () -> configurator.build().send(ctx).join());

        assertSame(expected, actual.getCause());
        assertTrue(ctx.calls.isEmpty());
    }

    @Test
    void filtersObserveThePipelineCancellationToken() {
        CancellationTokenSource source = new CancellationTokenSource();
        source.cancel();
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        configurator.useExecute(context -> {
            assertSame(source.getToken(), context.getCancellationToken());
            assertTrue(context.getCancellationToken().isCancelled());
            return CompletableFuture.completedFuture(null);
        });

        configurator.build().send(new TestContext(source.getToken())).join();
    }

    @Test
    void describesFilterOrderLifetimeAndConfigurationWithoutInstances() {
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        configurator.useExecute(context -> CompletableFuture.completedFuture(null));
        configurator.useRetry(2, java.time.Duration.ofMillis(25));
        configurator.useScopedFilter(ScopedTestFilter.class);

        PipelineDescriptor descriptor = configurator.getDescriptor();

        assertEquals(PipelineDescriptor.CURRENT_VERSION, descriptor.version());
        assertEquals(3, descriptor.filters().size());
        FilterDescriptor execute = descriptor.filters().get(0);
        assertEquals(0, execute.order());
        assertEquals("execute", execute.kind());
        assertNull(execute.implementation());
        assertEquals(FilterLifetime.INSTANCE, execute.lifetime());

        FilterDescriptor retry = descriptor.filters().get(1);
        assertEquals(1, retry.order());
        assertEquals("retry", retry.kind());
        assertEquals(FilterLifetime.PIPE, retry.lifetime());
        assertEquals("2", retry.configuration().get("retryCount"));
        assertEquals("25", retry.configuration().get("delayMilliseconds"));

        FilterDescriptor scoped = descriptor.filters().get(2);
        assertEquals(2, scoped.order());
        assertEquals("filter", scoped.kind());
        assertTrue(scoped.implementation().endsWith("ScopedTestFilter"));
        assertEquals(FilterLifetime.SCOPED, scoped.lifetime());

        assertThrows(UnsupportedOperationException.class, () -> descriptor.filters().add(execute));
        assertThrows(UnsupportedOperationException.class, () -> retry.configuration().put("changed", "true"));
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
        ServiceCollection services = ServiceCollection.create();
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

    @Test
    void scopedFilterIsDisposedAfterAsyncPipelineCompletion() {
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(ScopedFilterState.class);
        services.addScoped(ScopedTestFilter.class);
        ServiceProvider provider = services.buildServiceProvider();
        ScopedFilterState state = provider.getRequiredService(ScopedFilterState.class);
        PipeConfigurator<TestContext> configurator = new PipeConfigurator<>();
        configurator.useScopedFilter(ScopedTestFilter.class);
        Pipe<TestContext> pipe = configurator.build(provider);

        CompletableFuture<Void> result = pipe.send(new TestContext());
        assertFalse(result.isDone());
        assertFalse(state.disposed);

        state.completion.complete(null);
        result.join();

        assertTrue(state.disposed);
    }
}
