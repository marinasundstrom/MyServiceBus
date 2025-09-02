package com.myservicebus;

public class SnakeCaseEndpointNameFormatter implements EndpointNameFormatter {
    public static final SnakeCaseEndpointNameFormatter INSTANCE = new SnakeCaseEndpointNameFormatter();

    @Override
    public String format(Class<?> messageType) {
        String name = messageType.getSimpleName();
        return name.replaceAll("([a-z0-9])([A-Z])", "$1_$2").toLowerCase();
    }
}
