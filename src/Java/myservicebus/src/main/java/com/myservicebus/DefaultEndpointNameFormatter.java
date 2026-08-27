package com.myservicebus;

public class DefaultEndpointNameFormatter implements EndpointNameFormatter {
    public static final DefaultEndpointNameFormatter INSTANCE = new DefaultEndpointNameFormatter();

    @Override
    public String format(Class<?> messageType) {
        return trimConsumerSuffix(messageType.getSimpleName());
    }

    static String trimConsumerSuffix(String name) {
        for (String suffix : new String[] { "Consumer", "Saga", "Activity" }) {
            if (name.endsWith(suffix)) {
                return name.substring(0, name.length() - suffix.length());
            }
        }
        return name;
    }
}
