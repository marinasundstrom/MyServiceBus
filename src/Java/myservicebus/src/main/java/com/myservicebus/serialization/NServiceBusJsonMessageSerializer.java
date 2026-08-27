package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.PropertyNamingStrategies;

import java.io.IOException;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.Map;
import java.util.UUID;

public final class NServiceBusJsonMessageSerializer implements MessageSerializer {
    private static final DateTimeFormatter SENT_TIME_FORMAT =
            DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss:SSSSSS 'Z'");
    private final ObjectMapper mapper = new ObjectMapper()
            .setPropertyNamingStrategy(PropertyNamingStrategies.UPPER_CAMEL_CASE)
            .findAndRegisterModules();

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.RAW_JSON_CONTENT_TYPE;
    }

    @Override
    public MessageEnvelopeMode getEnvelopeMode() {
        return MessageEnvelopeMode.RAW;
    }

    @Override
    public <T> byte[] serialize(MessageSerializationContext<T> context) throws IOException {
        Map<String, Object> headers = context.getHeaders();
        UUID messageId = context.getRequestId() != null ? context.getRequestId() : context.getMessageId();
        Class<?> messageClass = context.getMessage().getClass();
        NServiceBusMessageType annotation = messageClass.getAnnotation(NServiceBusMessageType.class);
        String messageType = annotation != null ? annotation.value() : messageClass.getName();

        putIfAbsent(headers, NServiceBusHeaders.CONTENT_TYPE, getContentType());
        putIfAbsent(headers, NServiceBusHeaders.ENCLOSED_MESSAGE_TYPES, messageType);
        putIfAbsent(headers, NServiceBusHeaders.MESSAGE_ID, messageId.toString());
        putIfAbsent(headers, NServiceBusHeaders.MESSAGE_INTENT, context.getIntent().getHeaderValue());
        putIfAbsent(headers, NServiceBusHeaders.CONVERSATION_ID,
                (context.getConversationId() != null ? context.getConversationId() : messageId).toString());
        putIfAbsent(headers, NServiceBusHeaders.TIME_SENT,
                SENT_TIME_FORMAT.format(context.getSentTime().atZoneSameInstant(ZoneOffset.UTC)));

        if (context.getCorrelationId() != null) {
            putIfAbsent(headers, NServiceBusHeaders.CORRELATION_ID, context.getCorrelationId().toString());
        }
        if (context.getResponseAddress() != null) {
            putIfAbsent(headers, NServiceBusHeaders.REPLY_TO_ADDRESS, formatAddress(context.getResponseAddress().toString()));
        }
        if (context.getIntent() == MessageIntent.REPLY && context.getRequestId() != null) {
            putIfAbsent(headers, NServiceBusHeaders.RELATED_TO, context.getRequestId().toString());
        }

        headers.put("_content_type", getContentType());
        headers.put("_message_id", messageId.toString());
        return mapper.writeValueAsBytes(context.getMessage());
    }

    private static void putIfAbsent(Map<String, Object> headers, String name, String value) {
        headers.putIfAbsent(name, value);
    }

    private static String formatAddress(String address) {
        return address.startsWith("queue:") ? address.substring("queue:".length()) : address;
    }
}
