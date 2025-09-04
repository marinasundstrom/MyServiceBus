package com.myservicebus;

public interface MessageBus extends PublishEndpoint, PublishEndpointProvider, SendEndpointProvider {
    void start() throws Exception;

    void stop() throws Exception;
}
