package com.myservicebus.serialization;

import com.myservicebus.SendContext;

public interface MessageSerializer {
    byte[] serialize(SendContext context) throws Exception;
}
