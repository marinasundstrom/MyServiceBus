package com.myservicebus.serialization;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.PropertyNamingStrategies;
import com.myservicebus.HostInfo;
import com.myservicebus.MessageUrn;
import java.time.OffsetDateTime;
import java.util.HashMap;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class SerializerContractTest {
    public static class TestMessage {
        public String text;
    }

    public static class ConfiguredMessage {
        public String camelText;
    }

    @Test
    void envelopeFactoryCreatesMatchingSerializerAndDeserializer() throws Exception {
        SerializerFactory factory = new EnvelopeSerializerFactory();
        MessageSerializer serializer = factory.createSerializer();
        MessageDeserializer deserializer = factory.createDeserializer();

        assertEquals(factory.getContentType(), serializer.getContentType());
        assertEquals(factory.getContentType(), deserializer.getContentType());

        TestMessage message = new TestMessage();
        message.text = "hello";
        MessageSerializationContext<TestMessage> context = createContext(message);
        MessageBody body = serializer.getMessageBody(context);
        InboundMessage inbound = deserializer.deserialize(body, new HashMap<>());

        assertEquals("hello", inbound.<TestMessage>getMessage(TestMessage.class).text);
    }

    @Test
    void rawJsonFactoryCreatesMatchingSerializerAndDeserializer() throws Exception {
        SerializerFactory factory = new RawJsonSerializerFactory();
        MessageSerializer serializer = factory.createSerializer();
        MessageDeserializer deserializer = factory.createDeserializer();

        TestMessage message = new TestMessage();
        message.text = "hello";
        MessageSerializationContext<TestMessage> context = createContext(message);
        MessageBody body = serializer.getMessageBody(context);
        InboundMessage inbound = deserializer.deserialize(body, context.getHeaders());

        assertEquals(factory.getContentType(), serializer.getContentType());
        assertEquals(factory.getContentType(), deserializer.getContentType());
        assertEquals("hello", inbound.<TestMessage>getMessage(TestMessage.class).text);
    }

    @Test
    void jsonDeserializerConvertsTextToMessageBody() {
        MessageDeserializer deserializer = new RawJsonMessageDeserializer();

        MessageBody body = deserializer.getMessageBody("{\"text\":\"hello\"}");

        assertEquals("{\"text\":\"hello\"}", body.getString());
    }

    @Test
    void jsonFactoriesUseApplicationObjectMapperOnSendAndReceive() throws Exception {
        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        mapper.setPropertyNamingStrategy(PropertyNamingStrategies.SNAKE_CASE);

        for (SerializerFactory factory : List.of(
                new EnvelopeSerializerFactory(mapper),
                new RawJsonSerializerFactory(mapper))) {
            ConfiguredMessage message = new ConfiguredMessage();
            message.camelText = "configured";
            MessageSerializationContext<ConfiguredMessage> context = createContext(message);

            MessageBody body = factory.createSerializer().getMessageBody(context);
            InboundMessage inbound = factory.createDeserializer().deserialize(body, new HashMap<>());

            assertTrue(body.getString().contains("\"camel_text\":\"configured\""));
            assertEquals("configured", inbound.<ConfiguredMessage>getMessage(ConfiguredMessage.class).camelText);
        }
    }

    private static <T> MessageSerializationContext<T> createContext(T message) {
        MessageSerializationContext<T> context = new MessageSerializationContext<>(message);
        context.setMessageId(UUID.randomUUID());
        context.setConversationId(UUID.randomUUID());
        context.setMessageType(List.of(MessageUrn.forClass(message.getClass())));
        context.setHeaders(new HashMap<>());
        context.setSentTime(OffsetDateTime.now());
        context.setHostInfo(new HostInfo());
        return context;
    }
}
