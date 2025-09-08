package com.myservicebus;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

public class NamingConventionsTest {
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
        String urn = NamingConventions.getMessageUrn(SampleUrnMessage.class);
        assertEquals("urn:message:TestApp:SampleUrnMessage", urn);
    }

    @Test
    void getExchangeNameUsesAttribute() {
        String name = NamingConventions.getExchangeName(AttributeMessage.class);
        assertEquals("custom-entity", name);
    }

    @Test
    void getExchangeNameUsesFormatter() {
        MessageEntityNameFormatter original = NamingConventions.getEntityNameFormatter();
        try {
            NamingConventions.setEntityNameFormatter(new StaticFormatter());
            String name = NamingConventions.getExchangeName(SampleUrnMessage.class);
            assertEquals("fmt-sampleurnmessage", name);
        } finally {
            NamingConventions.setEntityNameFormatter(original);
        }
    }
}
