package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.HostInfoProvider;
import com.myservicebus.NamingConventions;
import com.myservicebus.SendContext;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

public class EnvelopeMessageSerializer implements MessageSerializer {
    private final ObjectMapper mapper;

    public EnvelopeMessageSerializer() {
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public byte[] serialize(SendContext context) throws Exception {
        context.getHeaders().put("content_type", "application/vnd.mybus.envelope+json");
        Envelope<Object> envelope = new Envelope<>();
        envelope.setMessageId(UUID.randomUUID());
        envelope.setSentTime(OffsetDateTime.now());
        envelope.setMessageType(List.of(NamingConventions.getMessageUrn(context.getMessage().getClass())));
        envelope.setMessage(context.getMessage());
        envelope.setHeaders(context.getHeaders());
        envelope.setContentType("application/json");
        envelope.setHost(HostInfoProvider.capture());
        return mapper.writeValueAsBytes(envelope);
    }
}
