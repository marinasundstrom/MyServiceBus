import static org.junit.jupiter.api.Assertions.assertNotSame;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

public class AotServiceCollectionTest {
    @Test
    void factoryRegistrationsResolveWithoutConstructorActivation() {
        ServiceCollection services = ServiceCollection.createAot();
        Dependency dependency = new Dependency();
        services.addSingleton(Dependency.class, () -> dependency);
        services.addScoped(Processor.class, provider -> () ->
                new Processor(provider.getRequiredService(Dependency.class)));
        ServiceProvider provider = services.buildServiceProvider();

        Processor first;
        try (ServiceScope scope = provider.createScope()) {
            first = scope.getServiceProvider().getRequiredService(Processor.class);
            assertSame(dependency, first.dependency);
            assertSame(first, scope.getServiceProvider().getRequiredService(Processor.class));
        }

        try (ServiceScope scope = provider.createScope()) {
            Processor second = scope.getServiceProvider().getRequiredService(Processor.class);
            assertNotSame(first, second);
        }
    }

    @Test
    void factoriesCanBridgeAnExistingContainerWithoutExposingServiceProvider() {
        ServiceCollection services = ServiceCollection.createAot();
        ExistingContainer container = new ExistingContainer();
        services.addScoped(Dependency.class, container::resolveDependency);
        ServiceProvider provider = services.buildServiceProvider();

        try (ServiceScope scope = provider.createScope()) {
            assertSame(container.dependency,
                    scope.getServiceProvider().getRequiredService(Dependency.class));
        }
    }

    @Test
    void jsr330ProvidersAdaptThroughTheStandardSupplierBoundary() {
        ServiceCollection services = ServiceCollection.createAot();
        Dependency dependency = new Dependency();
        javax.inject.Provider<Dependency> provider = () -> dependency;
        services.addScoped(Dependency.class, provider::get);
        ServiceProvider serviceProvider = services.buildServiceProvider();

        try (ServiceScope scope = serviceProvider.createScope()) {
            assertSame(dependency,
                    scope.getServiceProvider().getRequiredService(Dependency.class));
        }
    }

    @Test
    void classRegistrationsRequireAnExplicitFactory() {
        ServiceCollection services = ServiceCollection.createAot();
        services.addSingleton(Dependency.class);
        ServiceProvider provider = services.buildServiceProvider();

        IllegalStateException exception = assertThrows(
                IllegalStateException.class,
                () -> provider.getRequiredService(Dependency.class));
        org.junit.jupiter.api.Assertions.assertTrue(
                exception.getMessage().contains("requires an explicit provider factory"));
    }

    private static final class Dependency {
    }

    private static final class Processor {
        private final Dependency dependency;

        Processor(Dependency dependency) {
            this.dependency = dependency;
        }
    }

    private static final class ExistingContainer {
        private final Dependency dependency = new Dependency();

        Dependency resolveDependency() {
            return dependency;
        }
    }
}
