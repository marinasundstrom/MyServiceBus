package com.myservicebus.serialization.bson;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.module.SimpleModule;
import com.fasterxml.jackson.databind.node.ObjectNode;
import com.myservicebus.serialization.ByteArrayMessageBody;
import com.myservicebus.serialization.InboundMessage;
import com.myservicebus.serialization.MessageBody;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.serialization.MessageHeaderConvention;
import de.undercouch.bson4jackson.BsonFactory;
import java.io.IOException;
import java.util.Base64;
import java.util.Map;
import java.util.UUID;

public final class BsonMessageDeserializer implements MessageDeserializer {
    private final ObjectMapper applicationMapper;
    private final ObjectMapper bsonMapper;
    private final MessageHeaderConvention headerConvention;

    public BsonMessageDeserializer(ObjectMapper applicationMapper, MessageHeaderConvention headerConvention) {
        if (applicationMapper == null) {
            throw new IllegalArgumentException("applicationMapper must not be null");
        }
        if (headerConvention == null) {
            throw new IllegalArgumentException("headerConvention must not be null");
        }
        SimpleModule dotNetBsonModule = new SimpleModule();
        dotNetBsonModule.addDeserializer(UUID.class, new DotNetGuidDeserializer());
        this.applicationMapper = applicationMapper.copy().registerModule(dotNetBsonModule);
        this.bsonMapper = new ObjectMapper(new BsonFactory());
        this.headerConvention = headerConvention;
    }

    @Override
    public String getContentType() {
        return BsonSerializerFactory.BSON_CONTENT_TYPE;
    }

    @Override
    public InboundMessage deserialize(MessageBody body, Map<String, Object> headers) throws IOException {
        try {
            ObjectNode envelope = (ObjectNode) bsonMapper.readTree(body.getBytes());
            if (envelope == null) {
                throw new IOException("The MassTransit BSON envelope was not found.");
            }
            return new BsonInboundMessage(envelope, headers, applicationMapper, headerConvention);
        } catch (Exception exception) {
            if (exception instanceof BsonSerializationException bsonException) {
                throw bsonException;
            }
            throw new BsonSerializationException(
                    "Failed to deserialize the MassTransit BSON envelope.",
                    exception);
        }
    }

    @Override
    public MessageBody getMessageBody(String text) {
        try {
            return new ByteArrayMessageBody(Base64.getDecoder().decode(text));
        } catch (IllegalArgumentException exception) {
            throw new BsonSerializationException("The BSON message body is not valid Base64.", exception);
        }
    }
}
