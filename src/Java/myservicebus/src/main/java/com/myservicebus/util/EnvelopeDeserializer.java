package com.myservicebus.util;

import java.io.IOException;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.Fault;

public class EnvelopeDeserializer {

    private static final ObjectMapper objectMapper = new ObjectMapper();

    /**
     * Deserialize an Envelope with a known message type using TypeReference.
     *
     * @param json  the raw JSON string
     * @param clazz the message class type
     * @param <T>   the message type
     * @return deserialized Envelope<T>
     * @throws JsonProcessingException if deserialization fails
     */
    public static <T> Envelope<T> deserialize(String json, Class<T> clazz) throws JsonProcessingException {
        JavaType type = objectMapper
                .getTypeFactory()
                .constructParametricType(Envelope.class, clazz);
        return objectMapper.readValue(json, type);
    }

    /**
     * Deserialize from raw bytes (e.g., RabbitMQ message body).
     *
     * @param data  the raw JSON bytes
     * @param clazz the message class type
     * @param <T>   the message type
     * @return deserialized Envelope<T>
     * @throws IOException
     */
    public static <T> Envelope<T> deserialize(byte[] data, Class<T> clazz) throws IOException {
        JavaType type = objectMapper
                .getTypeFactory()
                .constructParametricType(Envelope.class, clazz);
        return objectMapper.readValue(data, type);
    }

    /*
     * public static <T> Envelope<Fault<T>> deserializeFault(String json, Class<T>
     * messageType)
     * throws JsonProcessingException {
     * JavaType faultType = objectMapper
     * .getTypeFactory()
     * .constructParametricType(Fault.class, messageType);
     * 
     * JavaType envelopeType = objectMapper
     * .getTypeFactory()
     * .constructParametricType(Envelope.class, faultType);
     * 
     * return objectMapper.readValue(json, envelopeType);
     * }
     */

    public static Object deserializeAndUnwrapFault(String json) throws Exception {
        ObjectMapper mapper = new ObjectMapper();

        // Step 1: Inspect envelope
        Envelope<Object> base = mapper.readValue(json, new TypeReference<Envelope<Object>>() {
        });
        String faultUrn = base.getMessageType().stream()
                .filter(s -> s.contains("Fault`1[["))
                .findFirst().orElse(null);

        if (faultUrn != null) {
            String className = extractInnerMessageTypeFromUrn(faultUrn);
            Class<?> innerClass = Class.forName(className);

            JavaType faultType = mapper.getTypeFactory().constructParametricType(Fault.class, innerClass);
            JavaType envelopeType = mapper.getTypeFactory().constructParametricType(Envelope.class, faultType);

            Envelope<?> typed = mapper.readValue(json, envelopeType);
            Fault<?> fault = (Fault<?>) typed.getMessage();
            return fault.getMessage();
        } else {
            // Not a fault — deserialize normally
            return base.getMessage();
        }
    }

    public static String extractInnerMessageTypeFromUrn(String urn) {
        if (urn == null || !urn.contains("Fault`1[["))
            return null;
        int start = urn.indexOf("Fault`1[[") + "Fault`1[[".length();
        int end = urn.indexOf(",", start); // before assembly info
        return urn.substring(start, end).trim();
    }
}