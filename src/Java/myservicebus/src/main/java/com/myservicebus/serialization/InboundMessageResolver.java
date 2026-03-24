package com.myservicebus.serialization;

import com.myservicebus.TransportMessage;

public interface InboundMessageResolver {
    InboundMessage resolve(TransportMessage transportMessage) throws Exception;
}
