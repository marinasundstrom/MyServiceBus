package com.myservicebus;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.databind.JavaType;
import com.myservicebus.HostInfoProvider;
import java.time.OffsetDateTime;
import java.util.*;
import java.util.UUID;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

public class BatchHandlingTest {
    static class SampleMessage {
        private String value;
        public SampleMessage() {}
        public SampleMessage(String value) { this.value = value; }
        public String getValue() { return value; }
        public void setValue(String value) { this.value = value; }
    }

    @Test
    public void envelopeSerializerHandlesBatchMessage() throws Exception {
        Batch<SampleMessage> batch = new Batch<>(
            new SampleMessage("A"),
            new SampleMessage("B")
        );

        Envelope<Batch<SampleMessage>> envelope = new Envelope<>();
        envelope.setMessageId(UUID.randomUUID());
        envelope.setMessageType(List.of(
            NamingConventions.getMessageUrn(Batch.class),
            NamingConventions.getMessageUrn(SampleMessage.class)
        ));
        envelope.setHeaders(new HashMap<>());
        envelope.setSentTime(OffsetDateTime.now());
        envelope.setHost(HostInfoProvider.capture());
        envelope.setMessage(batch);

        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        mapper.disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        byte[] bytes = mapper.writeValueAsBytes(envelope);

        JsonNode root = mapper.readTree(bytes);
        JsonNode messageNode = root.get("message");
        Assertions.assertTrue(messageNode.isArray());
        Assertions.assertEquals(2, messageNode.size());

        JsonNode typeNode = root.get("messageType");
        Assertions.assertEquals(2, typeNode.size());
        List<String> types = new ArrayList<>();
        typeNode.forEach(n -> types.add(n.asText()));
        Assertions.assertTrue(types.contains(NamingConventions.getMessageUrn(Batch.class)));
        Assertions.assertTrue(types.contains(NamingConventions.getMessageUrn(SampleMessage.class)));

        JavaType batchType = mapper.getTypeFactory().constructParametricType(Batch.class, SampleMessage.class);
        JavaType envelopeType = mapper.getTypeFactory().constructParametricType(Envelope.class, batchType);
        Envelope<Batch<SampleMessage>> deserialized = mapper.readValue(bytes, envelopeType);

        Assertions.assertNotNull(deserialized.getMessage());
        Assertions.assertEquals(2, deserialized.getMessage().size());
        Assertions.assertEquals("A", deserialized.getMessage().get(0).getValue());
        Assertions.assertEquals("B", deserialized.getMessage().get(1).getValue());
    }
}
