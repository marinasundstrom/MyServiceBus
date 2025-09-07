package com.myservicebus.serialization;

import java.io.IOException;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeFormatterBuilder;
import java.time.temporal.ChronoField;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;
import java.lang.reflect.Type;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.myservicebus.Envelope;

public class EnvelopeMessageDeserializer implements MessageDeserializer {
    private final ObjectMapper mapper;

    public EnvelopeMessageDeserializer() {
        mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        mapper.disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);

        JavaTimeModule module = new JavaTimeModule();
        DateTimeFormatter formatter = new DateTimeFormatterBuilder()
                .appendPattern("yyyy-MM-dd'T'HH:mm:ss")
                .appendFraction(ChronoField.NANO_OF_SECOND, 0, 6, true)
                .appendOffset("+HH:MM", "Z")
                .toFormatter();
        module.addDeserializer(OffsetDateTime.class, new JsonDeserializer<>() {
            @Override
            public OffsetDateTime deserialize(JsonParser p, DeserializationContext ctxt) throws IOException {
                return OffsetDateTime.parse(p.getText(), formatter);
            }
        });
        mapper.registerModule(module);
    }

    @Override
    public <T> Envelope<T> deserialize(byte[] data, Type type) throws IOException {
        JavaType messageType = mapper.getTypeFactory().constructType(type);
        JavaType envelopeType = mapper.getTypeFactory().constructParametricType(Envelope.class, messageType);
        return mapper.readValue(data, envelopeType);
    }
}
