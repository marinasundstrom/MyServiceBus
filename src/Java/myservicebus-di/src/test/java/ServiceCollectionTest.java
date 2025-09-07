import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.LogLevel;
import com.myservicebus.logging.Logger;

import org.junit.jupiter.api.Test;

import java.util.Set;

import static org.junit.jupiter.api.Assertions.*;

public class ServiceCollectionTest {
    @Test
    void testSingletonSelfBinding() {
        ServiceCollection sc = new ServiceCollection();
        sc.addSingleton(ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        ProcessorImpl p1 = sp.getService(ProcessorImpl.class);
        ProcessorImpl p2 = sp.getService(ProcessorImpl.class);

        assertSame(p1, p2); // singleton
        assertEquals("processed", p1.process());
    }

    @Test
    void testSingletonInterfaceBinding() {
        ServiceCollection sc = new ServiceCollection();
        sc.addSingleton(Processor.class, ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p = sp.getService(Processor.class);
        assertEquals("processed", p.process());
    }

    @Test
    void testSingletonProviderBinding() {
        ServiceCollection sc = new ServiceCollection();
        sc.addSingleton(Processor.class, sp -> () -> new ProcessorImpl());
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p = sp.getService(Processor.class);
        assertEquals("processed", p.process());
    }

    @Test
    void testScopedSelfBinding() {
        ServiceCollection sc = new ServiceCollection();
        sc.addScoped(ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        ProcessorImpl p1 = null;

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            p1 = scopedSp.getService(ProcessorImpl.class);
        }

        ProcessorImpl p2 = null;

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            p2 = scopedSp.getService(ProcessorImpl.class);
        }

        assertNotSame(p1, p2);
    }

    @Test
    void testScopedInterfaceBinding() {
        ServiceCollection sc = new ServiceCollection();
        sc.addScoped(Processor.class, ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p1 = null;

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            p1 = scopedSp.getService(Processor.class);
        }

        Processor p2 = null;

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            p2 = scopedSp.getService(Processor.class);
        }

        assertNotSame(p1, p2);
    }

    @Test
    void testScopedProviderBinding() {
        ServiceCollection sc = new ServiceCollection();
        sc.addScoped(Processor.class, sp -> () -> new ProcessorImpl());
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p1 = null;

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            p1 = scopedSp.getService(Processor.class);
        }

        Processor p2 = null;

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            p2 = scopedSp.getService(Processor.class);
        }

        assertNotSame(p1, p2);
    }

    @Test
    public void testMultiBindingResolvesAllImplementations() {
        // Arrange
        ServiceCollection collection = new ServiceCollection();
        collection.addMultiBinding(Handler.class, HandlerA.class);
        collection.addMultiBinding(Handler.class, HandlerB.class);

        // Act
        ServiceProvider provider = collection.buildServiceProvider();
        Set<Handler> handlers = provider.getServices(Handler.class);

        // Assert
        assertNotNull(handlers);
        assertEquals(2, handlers.size());

        boolean hasA = handlers.stream().anyMatch(h -> h instanceof HandlerA);
        boolean hasB = handlers.stream().anyMatch(h -> h instanceof HandlerB);

        assertTrue(hasA, "Should contain HandlerA");
        assertTrue(hasB, "Should contain HandlerB");
    }

    @Test
    void testScopedMultiBinding() {
        ServiceCollection sc = new ServiceCollection();
        sc.addScopedMultiBinding(Processor.class, ProcessorImpl.class);
        sc.addScopedMultiBinding(Processor.class, AnotherProcessor.class);
        ServiceProvider sp = sc.buildServiceProvider();

        Set<Processor> scopedSet1 = null;

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            scopedSet1 = scopedSp.getServices(Processor.class);
        }

        Set<Processor> scopedSet2 = null;

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            scopedSet2 = scopedSp.getServices(Processor.class);
        }

        assertEquals(2, scopedSet1.size());
        assertEquals(2, scopedSet2.size());
        assertNotSame(scopedSet1.iterator().next(), scopedSet2.iterator().next());
    }

    @Test
    void testGetRequiredServiceReturnsService() {
        ServiceCollection sc = new ServiceCollection();
        sc.addSingleton(Processor.class, ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p = sp.getRequiredService(Processor.class);
        assertEquals("processed", p.process());
    }

    @Test
    void testGetRequiredServiceThrowsWhenMissing() {
        ServiceProvider sp = new ServiceCollection().buildServiceProvider();

        assertThrows(IllegalStateException.class, () -> sp.getRequiredService(Processor.class));
    }

    @Test
    void consoleLoggerCanBeConfigured() {
        ServiceCollection sc = new ServiceCollection();
        sc.addConsoleLogger(cfg -> cfg.setMinimumLevel(LogLevel.DEBUG));
        ServiceProvider sp = sc.buildServiceProvider();

        LoggerFactory factory = sp.getService(LoggerFactory.class);
        Logger logger = factory.create("test");

        assertTrue(logger.isDebugEnabled());
    }
}