package com.myservicebus.azure.servicebus;

import com.azure.messaging.servicebus.ServiceBusMessage;
import com.azure.messaging.servicebus.ServiceBusReceivedMessage;
import com.myservicebus.serialization.MassTransitHeaderConvention;
import com.myservicebus.serialization.MessageHeaderConvention;

import java.net.URI;
import java.util.HashMap;
import java.util.Map;

final class AzureServiceBusMessageMapper {
    private static final MessageHeaderConvention HEADER_CONVENTION = MassTransitHeaderConvention.INSTANCE;

    private AzureServiceBusMessageMapper() {
    }

    static ServiceBusMessage createMessage(byte[] body, Map<String, Object> headers, String contentType) {
        ServiceBusMessage message = new ServiceBusMessage(body);
        message.setContentType(contentType != null ? contentType : "application/vnd.masstransit+json");
        if (headers == null) {
            return message;
        }

        headers.forEach((key, value) -> {
            if (key.startsWith("_")) {
                applyNativeProperty(message, key.substring(1), value);
            } else {
                message.getApplicationProperties().put(key, normalize(value));
            }
        });
        return message;
    }

    static ServiceBusMessage copy(ServiceBusReceivedMessage received) {
        ServiceBusMessage copy = new ServiceBusMessage(received.getBody());
        copy.setContentType(received.getContentType());
        copy.setCorrelationId(received.getCorrelationId());
        copy.setMessageId(received.getMessageId());
        copy.setReplyTo(received.getReplyTo());
        copy.setReplyToSessionId(received.getReplyToSessionId());
        copy.setSessionId(received.getSessionId());
        copy.setSubject(received.getSubject());
        copy.setTo(received.getTo());
        copy.setTimeToLive(received.getTimeToLive());
        copy.getApplicationProperties().putAll(received.getApplicationProperties());
        return copy;
    }

    static Map<String, Object> createHeaders(ServiceBusReceivedMessage message, String faultAddress) {
        Map<String, Object> headers = new HashMap<>(message.getApplicationProperties());
        headers.put(HEADER_CONVENTION.getContentTypeHeader(),
                message.getContentType() != null
                        ? message.getContentType()
                        : "application/vnd.masstransit+json");
        if (message.getMessageId() != null) {
            headers.put("message_id", message.getMessageId());
        }
        if (message.getCorrelationId() != null) {
            headers.put("correlation_id", message.getCorrelationId());
        }
        if (message.getReplyTo() != null) {
            headers.put("reply_to", message.getReplyTo());
        }
        if (message.getSubject() != null) {
            headers.put("subject", message.getSubject());
        }
        if (message.getTo() != null) {
            headers.put("to", message.getTo());
        }
        if (message.getTimeToLive() != null) {
            headers.put("expiration", Long.toString(message.getTimeToLive().toMillis()));
        }
        if (faultAddress != null) {
            headers.putIfAbsent(HEADER_CONVENTION.getFaultAddressHeader(), faultAddress);
        }
        return headers;
    }

    private static void applyNativeProperty(ServiceBusMessage message, String key, Object value) {
        String text = value == null ? null : value.toString();
        switch (key) {
            case "content_type" -> message.setContentType(text);
            case "correlation_id" -> message.setCorrelationId(text);
            case "message_id" -> message.setMessageId(text);
            case "reply_to" -> message.setReplyTo(text);
            case "subject", "type" -> message.setSubject(text);
            case "to" -> message.setTo(text);
            case "expiration" -> {
                try {
                    long milliseconds = Long.parseLong(text);
                    if (milliseconds < 0) {
                        throw new NumberFormatException("negative expiration");
                    }
                    message.setTimeToLive(java.time.Duration.ofMillis(milliseconds));
                } catch (NumberFormatException exception) {
                    throw new IllegalArgumentException(
                            "Azure Service Bus expiration must be a non-negative millisecond value.", exception);
                }
            }
            default -> message.getApplicationProperties().put(key, normalize(value));
        }
    }

    private static Object normalize(Object value) {
        if (value == null) {
            return "";
        }
        if (value instanceof String || value instanceof Boolean || value instanceof Byte
                || value instanceof Short || value instanceof Integer || value instanceof Long
                || value instanceof Float || value instanceof Double || value instanceof byte[]) {
            return value;
        }
        if (value instanceof URI || value instanceof Enum<?>) {
            return value.toString();
        }
        return value.toString();
    }
}
