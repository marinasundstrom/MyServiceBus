import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

import static org.junit.jupiter.api.Assertions.*;

public class PerMessageScopeTest {
    @Test
    void scopedServiceReturnsSameInstanceWithinScope() {
        ServiceCollection sc = ServiceCollection.create();
        sc.addScoped(ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        ProcessorImpl first;
        try (ServiceScope scope = sp.createScope()) {
            var scoped = scope.getServiceProvider();
            first = scoped.getService(ProcessorImpl.class);
            ProcessorImpl again = scoped.getService(ProcessorImpl.class);
            assertSame(first, again);
        }

        ProcessorImpl second;
        try (ServiceScope scope = sp.createScope()) {
            var scoped = scope.getServiceProvider();
            second = scoped.getService(ProcessorImpl.class);
        }

        assertNotSame(first, second);
    }

    @Test
    void nestedScopesHaveIsolatedInstances() {
        ServiceCollection sc = ServiceCollection.create();
        sc.addScoped(ProcessorImpl.class);
        ServiceProvider sp = sc.buildServiceProvider();

        try (ServiceScope outer = sp.createScope()) {
            var outerSp = outer.getServiceProvider();
            ProcessorImpl outerInstance = outerSp.getService(ProcessorImpl.class);

            try (ServiceScope inner = outerSp.createScope()) {
                var innerSp = inner.getServiceProvider();
                ProcessorImpl innerInstance = innerSp.getService(ProcessorImpl.class);
                assertNotSame(outerInstance, innerInstance);
            }

            ProcessorImpl afterInner = outerSp.getService(ProcessorImpl.class);
            assertSame(outerInstance, afterInner);
        }
    }
}
