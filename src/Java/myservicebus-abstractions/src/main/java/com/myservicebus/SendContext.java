package com.myservicebus;

import com.myservicebus.serialization.MessageSerializationContext;
import com.myservicebus.serialization.MessageSerializer;
import java.time.OffsetDateTime;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import com.myservicebus.tasks.CancellationToken;

public class SendContext implements PipeContext {
    private Object message;
    private final Map<String, Object> headers = new HashMap<>();
    private final CancellationToken cancellationToken;

    public SendContext(Object message, CancellationToken cancellationToken) {
        this.message = message;
        this.cancellationToken = cancellationToken;
    }

    public Object getMessage() {
        return message;
    }

    public void setMessage(Object message) {
        this.message = message;
    }

    public Map<String, Object> getHeaders() {
        return headers;
    }

    public byte[] serialize(MessageSerializer serializer) throws Exception {
        MessageSerializationContext<Object> context = new MessageSerializationContext<>(message);
        context.setMessageId(UUID.randomUUID());
        context.setCorrelationId(null);
        context.setMessageType(List.of(NamingConventions.getMessageUrn(message.getClass())));
        context.setResponseAddress(null);
        context.setFaultAddress(null);
        context.setHeaders(headers);
        context.setSentTime(OffsetDateTime.now());
        context.setHostInfo(HostInfoProvider.capture());
        return serializer.serialize(context);
    }

    @Override
    public CancellationToken getCancellationToken() {
        return cancellationToken;
    }
}
