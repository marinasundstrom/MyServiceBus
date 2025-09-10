import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.di.ServiceDescriptor;
import com.myservicebus.di.ServiceLifetime;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.LogLevel;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.ConsoleLoggerConfig;
import com.myservicebus.logging.ConsoleLoggerFactory;

import org.junit.jupiter.api.Test;

import java.util.Set;

import static org.junit.jupiter.api.Assertions.*;

public class ServiceCollectionTest {
    @Test
    void testSingletonSelfBinding() {
        ServiceCollection sc = ServiceCollection.create();
        sc.addSingleton(ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        ProcessorImpl p1 = sp.getService(ProcessorImpl.class);
        ProcessorImpl p2 = sp.getService(ProcessorImpl.class);

        assertSame(p1, p2); // singleton
        assertEquals("processed", p1.process());
    }

    @Test
    void testSingletonInterfaceBinding() {
        ServiceCollection sc = ServiceCollection.create();
        sc.addSingleton(Processor.class, ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p = sp.getService(Processor.class);
        assertEquals("processed", p.process());
    }

    @Test
    void testSingletonProviderBinding() {
        ServiceCollection sc = ServiceCollection.create();
        sc.addSingleton(Processor.class, sp -> () -> new ProcessorImpl());
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p = sp.getService(Processor.class);
        assertEquals("processed", p.process());
    }

    @Test
    void testScopedSelfBinding() {
        ServiceCollection sc = ServiceCollection.create();
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
        ServiceCollection sc = ServiceCollection.create();
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
        ServiceCollection sc = ServiceCollection.create();
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
        ServiceCollection collection = ServiceCollection.create();
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
        ServiceCollection sc = ServiceCollection.create();
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
        ServiceCollection sc = ServiceCollection.create();
        sc.addSingleton(Processor.class, ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p = sp.getRequiredService(Processor.class);
        assertEquals("processed", p.process());
    }

    @Test
    void testGetRequiredServiceThrowsWhenMissing() {
        ServiceProvider sp = ServiceCollection.create().buildServiceProvider();

        assertThrows(IllegalStateException.class, () -> sp.getRequiredService(Processor.class));
    }

    @Test
    void consoleLoggerCanBeConfigured() {
        ServiceCollection sc = ServiceCollection.create();
        ConsoleLoggerConfig cfg = new ConsoleLoggerConfig();
        cfg.setMinimumLevel(LogLevel.DEBUG);
        sc.addSingleton(ConsoleLoggerConfig.class, sp -> () -> cfg);
        sc.addSingleton(LoggerFactory.class, sp -> () -> new ConsoleLoggerFactory(cfg));
        ServiceProvider sp = sc.buildServiceProvider();

        LoggerFactory factory = sp.getService(LoggerFactory.class);
        Logger logger = factory.create("test");

        assertTrue(logger.isDebugEnabled());
    }

    @Test
    void removedServiceIsNotResolved() {
        ServiceCollection sc = ServiceCollection.create();
        sc.addSingleton(Processor.class, ProcessorImpl.class);
        sc.remove(Processor.class);
        ServiceProvider sp = sc.buildServiceProvider();

        Processor p = sp.getService(Processor.class);
        assertNull(p);
    }

    @Test
    void removedDeferredServiceIsNotResolved() {
        ServiceCollection sc = ServiceCollection.create();
        sc.addScoped(Processor.class, sp -> () -> new ProcessorImpl());
        sc.remove(Processor.class);
        ServiceProvider sp = sc.buildServiceProvider();

        try (ServiceScope scope = sp.createScope()) {
            var scopedSp = scope.getServiceProvider();
            Processor p = scopedSp.getService(Processor.class);
            assertNull(p);
        }
    }

    @Test
    void serviceDescriptorsExposeRegistrations() {
        ServiceCollection sc = ServiceCollection.create();
        sc.addSingleton(Processor.class, ProcessorImpl.class);

        assertEquals(1, sc.getDescriptors().size());
        ServiceDescriptor d = sc.getDescriptors().get(0);
        assertEquals(Processor.class, d.getServiceType());
        assertEquals(ServiceLifetime.SINGLETON, d.getLifetime());
    }
}