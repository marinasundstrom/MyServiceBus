package com.myservicebus;

public class KebabCaseEndpointNameFormatter implements EndpointNameFormatter {
    public static final KebabCaseEndpointNameFormatter INSTANCE = new KebabCaseEndpointNameFormatter();

    @Override
    public String format(Class<?> messageType) {
        String name = messageType.getSimpleName();
        return name.replaceAll("([a-z0-9])([A-Z])", "$1-$2").toLowerCase();
    }
}
