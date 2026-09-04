package com.myservicebus;

public interface SendEndpointProvider extends OutgoingMessageDispatcherProvider {
    SendEndpoint getSendEndpoint(String uri);

    @Override
    default OutgoingMessageDispatcher getMessageDispatcher(String destination) {
        return getSendEndpoint(destination);
    }
}
