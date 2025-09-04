package com.myservicebus;

public interface MessageBus extends SendEndpoint, PublishEndpoint {
    void start() throws Exception;

    void stop() throws Exception;
}
