package com.myservicebus;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

public class NamingConventions {
    private static final Map<Class<?>, String> exchangeNameOverrides = new ConcurrentHashMap<>();

    public static String getMessageUrn(Class<?> messageType) {
        return String.format("urn:message:%s", getMessageName(messageType));
    }

    public static String getExchangeName(Class<?> messageType) {
        String override = exchangeNameOverrides.get(messageType);
        return override != null ? override : getMessageName(messageType);
    }

    public static void setExchangeName(Class<?> messageType, String name) {
        if (messageType != null && name != null) {
            exchangeNameOverrides.put(messageType, name);
        }
    }

    public static String getMessageName(Class<?> messageType) {
        return String.format("TestApp:%s", messageType.getSimpleName());
    }

    public static String getQueueName(Class<?> messageType) {
        return messageType.getName().toLowerCase().replace(".", "-") + "-consumer";
    }
}
