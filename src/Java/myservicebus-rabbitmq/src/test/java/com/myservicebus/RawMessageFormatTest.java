package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Slf4jLoggerFactory;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import com.myservicebus.rabbitmq.RabbitMqSendContextFactory;
import com.myservicebus.rabbitmq.RabbitMqSendEndpointProvider;
import com.myservicebus.rabbitmq.RabbitMqTransportFactory;
import com.myservicebus.serialization.RawJsonMessageSerializer;

class RawMessageFormatTest {
    static class TestMessage {
        private final String text;

        TestMessage(String text) {
            this.text = text;
        }

        public String getText() {
            return text;
        }
    }

    @Test
    void send_endpoint_uses_application_json_for_raw_messages() {
        CapturingFactory factory = new CapturingFactory();
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(
                factory,
                new SendPipe(ctx -> CompletableFuture.completedFuture(null)),
                new RawJsonMessageSerializer(),
                URI.create("rabbitmq://localhost/"),
                new RabbitMqSendContextFactory(),
                loggerFactory);

        provider.getSendEndpoint("queue:test").send(
                new TestMessage("hi"),
                ctx -> ctx.getHeaders().put("NServiceBus.EnclosedMessageTypes", "Contracts:TestMessage"),
                com.myservicebus.tasks.CancellationToken.none).join();

        assertEquals("application/json", factory.contentType);
        assertTrue(new String(factory.body, StandardCharsets.UTF_8).contains("\"text\":\"hi\""));
        assertEquals("Contracts:TestMessage", factory.headers.get("NServiceBus.EnclosedMessageTypes"));
    }

    @Test
    void publish_pipe_can_emit_raw_json_payload() {
        RawJsonMessageSerializer serializer = new RawJsonMessageSerializer();
        SendContext context = new SendContext(new TestMessage("hi"), com.myservicebus.tasks.CancellationToken.none);
        context.getHeaders().put("NServiceBus.EnclosedMessageTypes", "Contracts:TestMessage");

        byte[] body;
        try {
            body = context.serialize(serializer);
        } catch (Exception ex) {
            throw new RuntimeException(ex);
        }

        assertTrue(new String(body, StandardCharsets.UTF_8).contains("\"text\":\"hi\""));
        assertEquals("application/json", context.getHeaders().get("content_type"));
        assertEquals("Contracts:TestMessage", context.getHeaders().get("NServiceBus.EnclosedMessageTypes"));
    }

    static class CapturingFactory extends RabbitMqTransportFactory {
        byte[] body;
        Map<String, Object> headers;
        String contentType;
        SendTransport transport = new SendTransport() {
            @Override
            public void send(byte[] data, Map<String, Object> sendHeaders, String sendContentType) {
                body = data;
                headers = sendHeaders;
                contentType = sendContentType;
            }
        };

        CapturingFactory() {
            super(null, new RabbitMqFactoryConfigurator(), new Slf4jLoggerFactory());
        }

        @Override
        public SendTransport getSendTransport(String exchange, boolean durable, boolean autoDelete) {
            return transport;
        }

        @Override
        public SendTransport getQueueTransport(String queue, boolean durable, boolean autoDelete) {
            return transport;
        }
    }
}
