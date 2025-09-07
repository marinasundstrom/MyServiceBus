package com.myservicebus;

import java.util.Collections;
import java.util.EnumSet;

public interface Endpoint {
    <T> void send(T message) throws Exception;

    default Iterable<Envelope<Object>> readAsync() {
        return Collections.emptyList();
    }

    EnumSet<EndpointCapability> getCapabilities();
}
