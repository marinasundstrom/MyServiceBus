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
import java.nio.charset.StandardCharsets;
import java.util.Map;

public class EnvelopeMessageDeserializer implements MessageDeserializer {
    private final ObjectMapper mapper;
    private final MessageHeaderConvention headerConvention;

    public EnvelopeMessageDeserializer() {
        this(MassTransitHeaderConvention.INSTANCE);
    }

    public EnvelopeMessageDeserializer(MessageHeaderConvention headerConvention) {
        this.headerConvention = headerConvention;
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
    public String getContentType() {
        return DefaultInboundMessageResolver.ENVELOPE_CONTENT_TYPE;
    }

    @Override
    public MessageEnvelopeMode getEnvelopeMode() {
        return MessageEnvelopeMode.ENVELOPE;
    }

    @Override
    public InboundMessage deserialize(MessageBody body, Map<String, Object> headers) throws IOException {
        return new EnvelopeInboundMessage(body.getBytes(), headers, mapper, headerConvention);
    }

    @Override
    public MessageBody getMessageBody(String text) {
        return new ByteArrayMessageBody(text.getBytes(StandardCharsets.UTF_8));
    }
}
