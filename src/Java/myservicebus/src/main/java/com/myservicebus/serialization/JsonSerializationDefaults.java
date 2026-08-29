package com.myservicebus.serialization;

import java.io.IOException;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeFormatterBuilder;
import java.time.temporal.ChronoField;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;

final class JsonSerializationDefaults {
    private JsonSerializationDefaults() {
    }

    static ObjectMapper createObjectMapper() {
        ObjectMapper mapper = new ObjectMapper();
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
            public OffsetDateTime deserialize(JsonParser parser, DeserializationContext context) throws IOException {
                return OffsetDateTime.parse(parser.getText(), formatter);
            }
        });
        mapper.registerModule(module);
        return mapper;
    }
}
