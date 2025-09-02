package com.myservicebus;

public interface EndpointNameFormatter {
    String format(Class<?> messageType);
}
