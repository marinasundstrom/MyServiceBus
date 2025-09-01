package com.myservicebus.abstractions;

public interface ReceiveTransport {
    void onReceive(java.util.function.Supplier<byte[]> data);
}