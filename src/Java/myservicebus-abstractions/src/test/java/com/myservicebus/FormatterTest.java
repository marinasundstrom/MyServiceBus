package com.myservicebus;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

public class FormatterTest {
    static class SampleUrnMessage { }

    @EntityName("custom-entity")
    static class AttributeMessage { }

    static class StaticFormatter implements MessageEntityNameFormatter {
        @Override
        public String formatEntityName(Class<?> messageType) {
            return "fmt-" + messageType.getSimpleName().toLowerCase();
        }
    }

    @Test
    void getMessageUrnReturnsExpected() {
        String urn = MessageUrn.forClass(SampleUrnMessage.class);
        assertEquals("urn:message:TestApp:SampleUrnMessage", urn);
    }

    @Test
    void getExchangeNameUsesAttribute() {
        String name = EntityNameFormatter.format(AttributeMessage.class);
        assertEquals("custom-entity", name);
    }

    @Test
    void getExchangeNameUsesFormatter() {
        MessageEntityNameFormatter original = EntityNameFormatter.getFormatter();
        try {
            EntityNameFormatter.setFormatter(new StaticFormatter());
            String name = EntityNameFormatter.format(SampleUrnMessage.class);
            assertEquals("fmt-sampleurnmessage", name);
        } finally {
            EntityNameFormatter.setFormatter(original);
        }
    }
}
