package com.myservicebus;

import java.time.Duration;

public interface ReceiveTransport {
    void start() throws Exception;

    void stop() throws Exception;

    default void stop(Duration timeout) throws Exception {
        stop();
    }
}
