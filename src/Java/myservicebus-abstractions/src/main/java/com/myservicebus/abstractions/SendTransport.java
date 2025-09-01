package com.myservicebus.abstractions;

public interface SendTransport {
    void send(byte[] data);
}
