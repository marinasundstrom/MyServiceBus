package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;

public class RawJsonMessageSerializer implements MessageSerializer {
    private final ObjectMapper mapper;

    public RawJsonMessageSerializer() {
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public <T> byte[] serialize(MessageSerializationContext<T> context) throws IOException {
        context.getHeaders().put("content_type", "application/json");
        return mapper.writeValueAsBytes(context.getMessage());
    }
}
