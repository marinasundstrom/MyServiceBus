import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

import java.util.Set;

import static org.junit.jupiter.api.Assertions.*;

public class ConstructorInjectionTest {

    @Test
    public void testSetInjectionInConstructor() {
        ServiceCollection collection = ServiceCollection.create();
        collection.addMultiBinding(Handler.class, HandlerA.class);
        collection.addMultiBinding(Handler.class, HandlerB.class);
        collection.addSingleton(HandlerConsumer.class);

        ServiceProvider provider = collection.buildServiceProvider();

        HandlerConsumer consumer = provider.getService(HandlerConsumer.class);
        Set<Handler> handlers = consumer.getHandlers();

        assertNotNull(handlers, "Handlers set should not be null");
        assertEquals(2, handlers.size(), "Should contain two handlers");

        boolean hasA = handlers.stream().anyMatch(h -> h instanceof HandlerA);
        boolean hasB = handlers.stream().anyMatch(h -> h instanceof HandlerB);

        assertTrue(hasA, "Should contain HandlerA");
        assertTrue(hasB, "Should contain HandlerB");
    }
}