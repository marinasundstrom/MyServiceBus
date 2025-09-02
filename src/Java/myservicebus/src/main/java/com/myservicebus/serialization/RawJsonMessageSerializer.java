package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.SendContext;

public class RawJsonMessageSerializer implements MessageSerializer {
    private final ObjectMapper mapper;

    public RawJsonMessageSerializer() {
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public byte[] serialize(SendContext context) throws Exception {
        context.getHeaders().put("content_type", "application/json");
        return mapper.writeValueAsBytes(context.getMessage());
    }
}
