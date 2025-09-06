package com.myservicebus.serialization;

import static org.junit.jupiter.api.Assertions.*;

import com.myservicebus.serialization.RawJsonMessageSerializer;
import com.myservicebus.serialization.MessageSerializationContext;
import org.junit.jupiter.api.Test;

import java.util.HashMap;

public class RawJsonMessageSerializerTest {
    static class TestMessage { public String text; }

    @Test
    public void serializesMessage() throws Exception {
        RawJsonMessageSerializer serializer = new RawJsonMessageSerializer();
        TestMessage message = new TestMessage();
        message.text = "hi";
        MessageSerializationContext<TestMessage> context = new MessageSerializationContext<>(message);
        context.setHeaders(new HashMap<>());

        byte[] bytes = serializer.serialize(context);
        String json = new String(bytes);
        assertTrue(json.contains("\"text\":\"hi\""));
        assertEquals("application/json", context.getHeaders().get("content_type"));
    }
}
