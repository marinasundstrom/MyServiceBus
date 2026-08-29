package com.myservicebus.serialization;

import static org.junit.jupiter.api.Assertions.assertEquals;

import com.myservicebus.TransportMessage;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.time.OffsetDateTime;
import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

class NServiceBusJsonMessageSerializerTest {
    @NServiceBusMessageType("Contracts.SubmitOrder")
    static class TestMessage {
        public String text;
    }

    @Test
    void serializesPlainJsonWithNServiceBusMetadata() throws Exception {
        TestMessage message = new TestMessage();
        message.text = "hi";
        UUID messageId = UUID.randomUUID();
        MessageSerializationContext<TestMessage> context = new MessageSerializationContext<>(message);
        context.setMessageId(messageId);
        context.setConversationId(UUID.randomUUID());
        context.setSentTime(OffsetDateTime.now());
        context.setHeaders(new HashMap<>());
        context.setIntent(MessageIntent.PUBLISH);

        byte[] body = new NServiceBusJsonMessageSerializer().getMessageBody(context).getBytes();

        assertEquals("{\"Text\":\"hi\"}", new String(body, StandardCharsets.UTF_8));
        assertEquals("application/json", context.getHeaders().get(NServiceBusHeaders.CONTENT_TYPE));
        assertEquals("Contracts.SubmitOrder", context.getHeaders().get(NServiceBusHeaders.ENCLOSED_MESSAGE_TYPES));
        assertEquals(messageId.toString(), context.getHeaders().get(NServiceBusHeaders.MESSAGE_ID));
        assertEquals("Publish", context.getHeaders().get(NServiceBusHeaders.MESSAGE_INTENT));
        assertEquals("application/json", context.getHeaders().get("_content_type"));
        assertEquals(messageId.toString(), context.getHeaders().get("_message_id"));
    }

    @Test
    void resolvesNServiceBusJsonAndMetadataSeparatelyFromRawJson() throws Exception {
        UUID messageId = UUID.randomUUID();
        UUID correlationId = UUID.randomUUID();
        Map<String, Object> headers = new HashMap<>();
        headers.put(NServiceBusHeaders.CONTENT_TYPE, "application/json".getBytes(StandardCharsets.UTF_8));
        headers.put(NServiceBusHeaders.ENCLOSED_MESSAGE_TYPES,
                "Contracts.SubmitOrder, Contracts".getBytes(StandardCharsets.UTF_8));
        headers.put(NServiceBusHeaders.MESSAGE_ID, messageId.toString());
        headers.put(NServiceBusHeaders.CORRELATION_ID, correlationId.toString());
        headers.put(NServiceBusHeaders.REPLY_TO_ADDRESS, "replies");

        InboundMessage inbound = new DefaultInboundMessageResolver(new EnvelopeMessageDeserializer())
                .resolve(new TransportMessage("{\"text\":\"hi\"}".getBytes(StandardCharsets.UTF_8), headers));

        assertEquals(InboundMessageFormat.NSERVICEBUS_JSON, inbound.getFormat());
        assertEquals(messageId, inbound.getMessageId());
        assertEquals(messageId, inbound.getRequestId());
        assertEquals(correlationId, inbound.getCorrelationId());
        assertEquals("urn:message:Contracts:SubmitOrder", inbound.getMessageType());
        assertEquals("queue:replies", inbound.getResponseAddress());
        assertEquals("hi", inbound.<TestMessage>getMessage(TestMessage.class).text);
    }
}
