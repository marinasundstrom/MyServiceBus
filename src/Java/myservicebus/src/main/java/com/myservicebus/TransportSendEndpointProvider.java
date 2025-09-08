package com.myservicebus;

import com.myservicebus.serialization.MessageSerializer;

public interface TransportSendEndpointProvider extends SendEndpointProvider {
    TransportSendEndpointProvider withSerializer(MessageSerializer serializer);
}
