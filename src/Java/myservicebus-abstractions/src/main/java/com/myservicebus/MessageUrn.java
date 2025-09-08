package com.myservicebus;

import java.lang.reflect.Proxy;

public final class MessageUrn {
    private MessageUrn() { }

    public static String forClass(Class<?> messageType) {
        if (Proxy.isProxyClass(messageType) && messageType.getInterfaces().length > 0) {
            messageType = messageType.getInterfaces()[0];
        }
        return String.format("urn:message:TestApp:%s", messageType.getSimpleName());
    }
}
