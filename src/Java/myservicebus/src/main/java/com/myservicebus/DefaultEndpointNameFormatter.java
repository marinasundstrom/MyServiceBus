package com.myservicebus;

public class DefaultEndpointNameFormatter implements EndpointNameFormatter {
    public static final DefaultEndpointNameFormatter INSTANCE = new DefaultEndpointNameFormatter();

    @Override
    public String format(Class<?> messageType) {
        return messageType.getSimpleName();
    }
}
