package com.myservicebus;

import java.lang.reflect.Proxy;

public class NamingConventions {

    public static String getMessageUrn(Class<?> messageType) {
        return String.format("urn:message:%s", getMessageName(messageType));
    }

    public static String getExchangeName(Class<?> messageType) {
        return getMessageName(messageType);
    }

    public static String getMessageName(Class<?> messageType) {
        if (Proxy.isProxyClass(messageType) && messageType.getInterfaces().length > 0) {
            messageType = messageType.getInterfaces()[0];
        }
        return String.format("TestApp:%s", messageType.getSimpleName());
    }

    public static String getQueueName(Class<?> messageType) {
        return messageType.getName().toLowerCase().replace(".", "-") + "-consumer";
    }
}
