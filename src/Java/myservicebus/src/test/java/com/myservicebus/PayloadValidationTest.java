package com.myservicebus;

import com.myservicebus.serialization.*;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

class PayloadValidationTest {
    record LocalOrder(int quantity) { }
    interface Order { int getQuantity(); }
    @Test
    void invalidEnvelopeIsADeserializationFailure() {
        var deserializer = new EnvelopeMessageDeserializer();
        for (String json : List.of("null", "[]", "{bad-json")) {
            assertThrows(MessageDeserializationException.class,
                    () -> deserializer.deserialize(deserializer.getMessageBody(json), Map.of()));
        }
    }

    @Test
    void interfaceBodiesAreValidatedBeforeConsumption() throws Exception {
        var deserializer = new EnvelopeMessageDeserializer();
        var inbound = deserializer.deserialize(deserializer.getMessageBody("{\"message\":{\"quantity\":3}}"), Map.of());
        Order order = inbound.getMessage(Order.class);
        assertEquals(3, order.getQuantity());
        var invalid = deserializer.deserialize(deserializer.getMessageBody("{\"message\":{\"quantity\":\"bad\"}}"), Map.of());
        assertThrows(MessageDeserializationException.class, () -> invalid.getMessage(Order.class));
        for (String json : List.of("{}", "{\"message\":null}")) {
            var empty = deserializer.deserialize(deserializer.getMessageBody(json), Map.of());
            assertThrows(MessageDeserializationException.class, () -> empty.getMessage(LocalOrder.class));
        }
    }
}
