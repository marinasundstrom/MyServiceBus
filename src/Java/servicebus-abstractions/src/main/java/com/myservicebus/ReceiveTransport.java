package com.myservicebus;

public interface ReceiveTransport {
    void onReceive(java.util.function.Supplier<byte[]> data);
}