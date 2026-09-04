package com.myservicebus;

import com.myservicebus.core.OutgoingMessageDispatcher;
import com.myservicebus.core.OutgoingMessageDispatcherProvider;

public interface SendEndpointProvider extends OutgoingMessageDispatcherProvider {
    SendEndpoint getSendEndpoint(String uri);

    @Override
    default OutgoingMessageDispatcher getMessageDispatcher(String destination) {
        return getSendEndpoint(destination);
    }
}
