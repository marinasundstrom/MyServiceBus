package com.myservicebus;

public interface ReceiveTransport {
    void start() throws Exception;

    void stop() throws Exception;
}