package com.myservicebus;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.serialization.EnvelopeMessageDeserializer;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;

public class EnvelopeMessageDeserializerTest {
    static class InnerMessage {
        private String text;
        public InnerMessage() {}
        public InnerMessage(String text) { this.text = text; }
        public String getText() { return text; }
        public void setText(String text) { this.text = text; }
    }

    @Test
    public void deserializesFaultWithTypedMessage() throws Exception {
        InnerMessage inner = new InnerMessage("oops");
        Fault<InnerMessage> fault = new Fault<>();
        fault.setMessage(inner);
        Envelope<Fault<InnerMessage>> envelope = new Envelope<>();
        envelope.setMessage(fault);
        envelope.setMessageType(List.of("urn:message:Fault", MessageUrn.forClass(InnerMessage.class)));
        envelope.setMessageId(UUID.randomUUID());

        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        byte[] data = mapper.writeValueAsBytes(envelope);

        EnvelopeMessageDeserializer deserializer = new EnvelopeMessageDeserializer();
        Envelope<Fault<InnerMessage>> result = deserializer.deserialize(data,
                new com.fasterxml.jackson.core.type.TypeReference<Fault<InnerMessage>>() {}.getType());

        assertEquals("oops", result.getMessage().getMessage().getText());
    }
}
