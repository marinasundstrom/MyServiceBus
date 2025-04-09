package com.myservicebus;

public class NamingConventions {

    public static String getMessageUrn(Class<?> messageType) {
        return String.format("urn:message:%s", getMessageName(messageType));
    }

    public static String getExchangeName(Class<?> messageType) {
        return getMessageName(messageType);
    }

    public static String getMessageName(Class<?> messageType) {
        return String.format("TestApp:%s", messageType.getSimpleName());
    }

    public static String getQueueName(Class<?> consumerType) {
        return consumerType.getName().toLowerCase().replace(".", "-") + "-consumer";
    }
}
