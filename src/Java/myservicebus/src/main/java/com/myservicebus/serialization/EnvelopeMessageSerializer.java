package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import java.io.IOException;
import java.util.HashMap;
import java.util.Map;

public class EnvelopeMessageSerializer implements MessageSerializer {
    private final ObjectMapper mapper;

    public EnvelopeMessageSerializer() {
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public <T> byte[] serialize(MessageSerializationContext<T> context) throws IOException {
        context.getHeaders().put("content_type", "application/vnd.masstransit+json");

        Map<String, Object> headers = new HashMap<>();
        context.getHeaders().forEach((k, v) -> {
            if (!k.startsWith("MT-Host-")) {
                headers.put(k, v);
            }
        });

        Envelope<T> envelope = new Envelope<>();
        envelope.setMessageId(context.getMessageId());
        envelope.setCorrelationId(context.getCorrelationId());
        envelope.setSourceAddress(
                context.getSourceAddress() != null ? context.getSourceAddress().toString() : null);
        envelope.setDestinationAddress(
                context.getDestinationAddress() != null ? context.getDestinationAddress().toString() : null);
        envelope.setResponseAddress(
                context.getResponseAddress() != null ? context.getResponseAddress().toString() : null);
        envelope.setFaultAddress(context.getFaultAddress() != null ? context.getFaultAddress().toString() : null);
        envelope.setSentTime(context.getSentTime());
        envelope.setMessageType(context.getMessageType());
        envelope.setMessage(context.getMessage());
        envelope.setHeaders(headers);
        envelope.setContentType("application/json");
        envelope.setHost(context.getHostInfo());
        return mapper.writeValueAsBytes(envelope);
    }
}
