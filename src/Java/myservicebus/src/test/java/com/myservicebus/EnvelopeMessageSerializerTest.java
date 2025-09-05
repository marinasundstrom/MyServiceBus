package com.myservicebus;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.MessageHeaders;
import org.junit.jupiter.api.Test;

import java.net.URI;

import static org.junit.jupiter.api.Assertions.*;

public class EnvelopeMessageSerializerTest {
    static class SampleMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }

    @Test
    public void envelopeContainsAddresses() throws Exception {
        SampleMessage message = new SampleMessage();
        message.setValue("Test");

        MessageSerializer serializer = new EnvelopeMessageSerializer();
        SendContext context = new SendContext(message, CancellationToken.none);

        byte[] bytes = context.serialize(serializer);

        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        Envelope<SampleMessage> envelope = mapper.readValue(bytes,
                mapper.getTypeFactory().constructParametricType(Envelope.class, SampleMessage.class));

        assertNotNull(envelope);
        assertEquals(URI.create("loopback://localhost/source").toString(), envelope.getSourceAddress());
        assertEquals(
                URI.create("loopback://localhost/" + SampleMessage.class.getSimpleName()).toString(),
                envelope.getDestinationAddress());
    }

    @Test
    public void envelopeOmitsMtHostHeaders() throws Exception {
        SampleMessage message = new SampleMessage();
        message.setValue("Test");

        MessageSerializer serializer = new EnvelopeMessageSerializer();
        SendContext context = new SendContext(message, CancellationToken.none);
        context.getHeaders().put(MessageHeaders.HOST_MACHINE, "machine");

        byte[] bytes = context.serialize(serializer);

        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        Envelope<SampleMessage> envelope = mapper.readValue(bytes,
                mapper.getTypeFactory().constructParametricType(Envelope.class, SampleMessage.class));

        assertNotNull(envelope);
        assertFalse(envelope.getHeaders().containsKey(MessageHeaders.HOST_MACHINE));
    }
}
