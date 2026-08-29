package com.myservicebus.serialization.bson;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.myservicebus.serialization.MassTransitHeaderConvention;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.serialization.MessageHeaderConvention;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.serialization.SerializerFactory;

public final class BsonSerializerFactory implements SerializerFactory {
    public static final String BSON_CONTENT_TYPE = "application/vnd.masstransit+bson";

    private final ObjectMapper applicationMapper;
    private final MessageHeaderConvention headerConvention;

    public BsonSerializerFactory() {
        this(createDefaultMapper(), MassTransitHeaderConvention.INSTANCE);
    }

    public BsonSerializerFactory(ObjectMapper applicationMapper) {
        this(applicationMapper, MassTransitHeaderConvention.INSTANCE);
    }

    public BsonSerializerFactory(
            ObjectMapper applicationMapper,
            MessageHeaderConvention headerConvention) {
        if (applicationMapper == null) {
            throw new IllegalArgumentException("applicationMapper must not be null");
        }
        if (headerConvention == null) {
            throw new IllegalArgumentException("headerConvention must not be null");
        }
        this.applicationMapper = applicationMapper;
        this.headerConvention = headerConvention;
    }

    @Override
    public String getContentType() {
        return BSON_CONTENT_TYPE;
    }

    @Override
    public MessageSerializer createSerializer() {
        return new BsonMessageSerializer(applicationMapper, headerConvention);
    }

    @Override
    public MessageDeserializer createDeserializer() {
        return new BsonMessageDeserializer(applicationMapper, headerConvention);
    }

    private static ObjectMapper createDefaultMapper() {
        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        mapper.disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        return mapper;
    }
}
