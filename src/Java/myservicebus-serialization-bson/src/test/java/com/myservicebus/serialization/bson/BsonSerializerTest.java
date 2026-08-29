package com.myservicebus.serialization.bson;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

import com.myservicebus.HostInfo;
import com.myservicebus.serialization.InboundMessage;
import com.myservicebus.serialization.InboundMessageFormat;
import com.myservicebus.serialization.MassTransitHeaderConvention;
import com.myservicebus.serialization.MessageBody;
import com.myservicebus.serialization.MessageSerializationContext;
import com.myservicebus.serialization.SerializerFactory;
import java.nio.charset.StandardCharsets;
import java.time.OffsetDateTime;
import java.util.Base64;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class BsonSerializerTest {
    @Test
    void readsTheDotNetBsonFixture() throws Exception {
        byte[] encoded = BsonSerializerTest.class.getClassLoader()
                .getResourceAsStream("serialization/v1/dotnet-bson-envelope.base64")
                .readAllBytes();
        byte[] body = Base64.getDecoder().decode(new String(encoded, StandardCharsets.UTF_8).trim());

        InboundMessage inbound = new BsonSerializerFactory()
                .createDeserializer()
                .deserialize(new com.myservicebus.serialization.ByteArrayMessageBody(body), Map.of());
        BsonTestMessage message = inbound.getMessage(BsonTestMessage.class);

        assertEquals(UUID.fromString("cf46535d-f7d4-451d-857f-9c64b64339da"), inbound.getCorrelationId());
        assertEquals(UUID.fromString("f8f53c23-1fbb-4f18-970d-3a6d27fd9c19"), message.getOrderId());
        assertEquals("1234.56", message.getTotal());
        assertEquals(2, ((Number) inbound.getHeaders().get("attempt")).intValue());
    }

    @Test
    void factoryRoundTripsTheMassTransitEnvelope() throws Exception {
        SerializerFactory factory = new BsonSerializerFactory();
        MessageSerializationContext<BsonTestMessage> context = createContext();

        MessageBody body = factory.createSerializer().getMessageBody(context);
        InboundMessage inbound = factory.createDeserializer().deserialize(
                body,
                Map.of("transport", "rabbitmq"));

        assertEquals(BsonSerializerFactory.BSON_CONTENT_TYPE, factory.getContentType());
        assertEquals(
                BsonSerializerFactory.BSON_CONTENT_TYPE,
                context.getHeaders().get(MassTransitHeaderConvention.INSTANCE.getContentTypeHeader()));
        assertEquals(BsonSerializerFactory.BSON_CONTENT_TYPE, inbound.getContentType());
        assertEquals(InboundMessageFormat.ENVELOPE, inbound.getFormat());
        assertEquals(context.getCorrelationId(), inbound.getCorrelationId());
        assertEquals(context.getConversationId(), inbound.getConversationId());
        assertEquals(context.getMessageType(), inbound.getMessageTypes());
        assertEquals("rabbitmq", inbound.getHeaders().get("transport"));
        assertEquals(2, ((Number) inbound.getHeaders().get("attempt")).intValue());

        BsonTestMessage message = inbound.getMessage(BsonTestMessage.class);
        assertNotNull(message);
        assertEquals(context.getMessage().getOrderId(), message.getOrderId());
        assertEquals(context.getMessage().getTotal(), message.getTotal());
    }

    @Test
    void textBodyUsesTheMassTransitBase64Convention() throws Exception {
        SerializerFactory factory = new BsonSerializerFactory();
        MessageBody body = factory.createSerializer().getMessageBody(createContext());
        String text = Base64.getEncoder().encodeToString(body.getBytes());

        MessageBody restored = factory.createDeserializer().getMessageBody(text);

        assertArrayEquals(body.getBytes(), restored.getBytes());
    }

    private static MessageSerializationContext<BsonTestMessage> createContext() {
        BsonTestMessage message = new BsonTestMessage();
        message.setOrderId(UUID.fromString("f8f53c23-1fbb-4f18-970d-3a6d27fd9c19"));
        message.setTotal("1234.56");
        MessageSerializationContext<BsonTestMessage> context = new MessageSerializationContext<>(message);
        context.setMessageId(UUID.fromString("124f4bc4-bc2f-45a7-bf9a-ddeba5aab587"));
        context.setCorrelationId(UUID.fromString("cf46535d-f7d4-451d-857f-9c64b64339da"));
        context.setConversationId(UUID.fromString("c7bba23f-49a4-40c4-869d-20e36a0dd38c"));
        context.setMessageType(List.of("urn:message:MyServiceBus.Tests:BsonTestMessage"));
        context.setHeaders(new HashMap<>(Map.of("attempt", 2)));
        context.setSentTime(OffsetDateTime.parse("2026-08-29T12:34:56.123456Z"));
        HostInfo host = new HostInfo();
        host.setMachineName("test");
        host.setProcessName("test");
        host.setProcessId(42);
        host.setAssembly("MyServiceBus.Tests");
        host.setAssemblyVersion("1.0.0");
        host.setFrameworkVersion("Java");
        host.setMassTransitVersion("1.0.0");
        host.setOperatingSystemVersion("test");
        context.setHostInfo(host);
        return context;
    }

    public static final class BsonTestMessage {
        private UUID orderId;
        private String total;

        public UUID getOrderId() {
            return orderId;
        }

        public void setOrderId(UUID orderId) {
            this.orderId = orderId;
        }

        public String getTotal() {
            return total;
        }

        public void setTotal(String total) {
            this.total = total;
        }
    }
}
