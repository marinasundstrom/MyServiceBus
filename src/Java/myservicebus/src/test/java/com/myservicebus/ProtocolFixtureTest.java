package com.myservicebus;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;

import java.io.InputStream;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

public class ProtocolFixtureTest {
    private final ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();

    @Test
    public void manifestListsValidEnvelopeFixtures() throws Exception {
        FixtureManifest manifest = read("manifest.json", FixtureManifest.class);

        assertEquals("1", manifest.protocolVersion);
        assertEquals("application/vnd.masstransit+json", manifest.contentType);
        assertEquals(List.of("message", "request", "fault"),
                manifest.fixtures.stream().map(x -> x.kind).toList());

        for (FixtureEntry fixture : manifest.fixtures) {
            JsonNode root = read(fixture.file, JsonNode.class);
            assertTrue(root.isObject());
            assertTrue(root.has("messageId"));
            assertTrue(root.has("messageType"));
            assertTrue(root.has("message"));
        }
    }

    @Test
    public void messageFixtureDeserializesWithPortableMetadata() throws Exception {
        Envelope<Map<String, Object>> envelope = read("message-envelope.json", new TypeReference<>() { });

        assertEquals(UUID.fromString("11111111-1111-1111-1111-111111111111"), envelope.getMessageId());
        assertEquals(UUID.fromString("22222222-2222-2222-2222-222222222222"), envelope.getCorrelationId());
        assertEquals("urn:message:MyServiceBus.Compatibility:SubmitOrder", envelope.getMessageType().get(0));
        assertEquals("C-123", envelope.getMessage().get("customerNumber"));
        assertEquals("tenant-a", envelope.getHeaders().get("tenant-id"));
        assertEquals("application/json", envelope.getContentType());
    }

    @Test
    public void requestFixtureDeserializesRequestAddressesAndIdentifiers() throws Exception {
        Envelope<Map<String, Object>> envelope = read("request-envelope.json", new TypeReference<>() { });

        assertEquals(UUID.fromString("66666666-6666-6666-6666-666666666666"), envelope.getRequestId());
        assertEquals("rabbitmq://localhost/order-api_bus_abc123", envelope.getResponseAddress());
        assertEquals(envelope.getResponseAddress(), envelope.getFaultAddress());
        assertNotNull(envelope.getExpirationTime());
    }

    @Test
    public void faultFixtureDeserializesTypedFaultContract() throws Exception {
        Envelope<Fault<Map<String, Object>>> envelope = read("fault-envelope.json", new TypeReference<>() { });

        assertEquals("urn:message:MassTransit:Fault[[MyServiceBus.Compatibility:GetOrderStatus]]",
                envelope.getMessageType().get(0));
        assertEquals(UUID.fromString("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), envelope.getMessage().getFaultId());
        assertEquals("Order was not found", envelope.getMessage().getExceptions().get(0).getMessage());
        assertEquals("44444444-4444-4444-4444-444444444444",
                envelope.getMessage().getMessage().get("orderId"));
    }

    private <T> T read(String fileName, Class<T> type) throws Exception {
        try (InputStream stream = fixture(fileName)) {
            return mapper.readValue(stream, type);
        }
    }

    private <T> T read(String fileName, TypeReference<T> type) throws Exception {
        try (InputStream stream = fixture(fileName)) {
            return mapper.readValue(stream, type);
        }
    }

    private InputStream fixture(String fileName) {
        InputStream stream = getClass().getResourceAsStream("/protocol/v1/" + fileName);
        if (stream == null) {
            throw new IllegalStateException("Protocol fixture not found: " + fileName);
        }
        return stream;
    }

    public static class FixtureManifest {
        public String protocolVersion;
        public String contentType;
        public List<FixtureEntry> fixtures;
    }

    public static class FixtureEntry {
        public String name;
        public String file;
        public String kind;
    }
}
