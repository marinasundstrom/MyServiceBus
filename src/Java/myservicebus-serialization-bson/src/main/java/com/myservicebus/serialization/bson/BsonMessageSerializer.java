package com.myservicebus.serialization.bson;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import com.myservicebus.serialization.ByteArrayMessageBody;
import com.myservicebus.serialization.MessageBody;
import com.myservicebus.serialization.MessageEnvelopeMode;
import com.myservicebus.serialization.MessageHeaderConvention;
import com.myservicebus.serialization.MessageSerializationContext;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.serialization.MessageSerializerMetadata;
import de.undercouch.bson4jackson.BsonFactory;
import java.io.IOException;
import java.time.format.DateTimeFormatter;
import java.util.Map;
import java.util.UUID;

public final class BsonMessageSerializer implements MessageSerializer, MessageSerializerMetadata {
    private final ObjectMapper applicationMapper;
    private final ObjectMapper bsonMapper;
    private final MessageHeaderConvention headerConvention;

    public BsonMessageSerializer(ObjectMapper applicationMapper, MessageHeaderConvention headerConvention) {
        if (applicationMapper == null) {
            throw new IllegalArgumentException("applicationMapper must not be null");
        }
        if (headerConvention == null) {
            throw new IllegalArgumentException("headerConvention must not be null");
        }
        this.applicationMapper = applicationMapper;
        this.bsonMapper = new ObjectMapper(new BsonFactory());
        this.headerConvention = headerConvention;
    }

    @Override
    public String getContentType() {
        return BsonSerializerFactory.BSON_CONTENT_TYPE;
    }

    @Override
    public MessageEnvelopeMode getEnvelopeMode() {
        return MessageEnvelopeMode.ENVELOPE;
    }

    @Override
    public <T> MessageBody getMessageBody(MessageSerializationContext<T> context) throws IOException {
        try {
            context.getHeaders().put(headerConvention.getContentTypeHeader(), getContentType());
            ObjectNode envelope = bsonMapper.createObjectNode();
            putUuid(envelope, "messageId", context.getMessageId());
            putUuid(envelope, "requestId", context.getRequestId());
            putUuid(envelope, "correlationId", context.getCorrelationId());
            putUuid(envelope, "conversationId", context.getConversationId());
            putUuid(envelope, "initiatorId", context.getInitiatorId());
            putText(envelope, "sourceAddress", context.getSourceAddress());
            putText(envelope, "destinationAddress", context.getDestinationAddress());
            putText(envelope, "responseAddress", context.getResponseAddress());
            putText(envelope, "faultAddress", context.getFaultAddress());
            if (context.getSentTime() != null) {
                envelope.put("sentTime", DateTimeFormatter.ISO_OFFSET_DATE_TIME.format(context.getSentTime()));
            }
            if (context.getMessageType() != null) {
                envelope.set("messageType", applicationMapper.valueToTree(context.getMessageType()));
            }
            envelope.set("message", applicationMapper.valueToTree(context.getMessage()));

            ObjectNode headers = bsonMapper.createObjectNode();
            for (Map.Entry<String, Object> entry : context.getHeaders().entrySet()) {
                if (!headerConvention.isHostHeader(entry.getKey())) {
                    JsonNode value = applicationMapper.valueToTree(entry.getValue());
                    headers.set(entry.getKey(), value);
                }
            }
            envelope.set("headers", headers);
            if (context.getHostInfo() != null) {
                envelope.set("host", applicationMapper.valueToTree(context.getHostInfo()));
            }

            return new ByteArrayMessageBody(bsonMapper.writeValueAsBytes(envelope));
        } catch (Exception exception) {
            if (exception instanceof BsonSerializationException bsonException) {
                throw bsonException;
            }
            throw new BsonSerializationException(
                    "Failed to serialize the message using the MassTransit BSON envelope.",
                    exception);
        }
    }

    private static void putUuid(ObjectNode envelope, String name, UUID value) {
        if (value != null) {
            envelope.put(name, value.toString());
        }
    }

    private static void putText(ObjectNode envelope, String name, Object value) {
        if (value != null) {
            envelope.put(name, value.toString());
        }
    }
}
