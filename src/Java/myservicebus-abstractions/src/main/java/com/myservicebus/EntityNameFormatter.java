package com.myservicebus;

import java.lang.reflect.Proxy;

public final class EntityNameFormatter {
    private static MessageEntityNameFormatter formatter = new DefaultMessageEntityNameFormatter();

    private EntityNameFormatter() { }

    public static MessageEntityNameFormatter getFormatter() {
        return formatter;
    }

    public static void setFormatter(MessageEntityNameFormatter f) {
        formatter = f;
    }

    public static String format(Class<?> messageType) {
        return format(messageType, formatter);
    }

    public static String format(Class<?> messageType, MessageEntityNameFormatter selectedFormatter) {
        if (Proxy.isProxyClass(messageType)) messageType = messageType.getInterfaces()[0];
        EntityName attr = messageType.getAnnotation(EntityName.class);
        if (attr != null)
            return attr.value();
        return (selectedFormatter != null ? selectedFormatter : formatter).formatEntityName(messageType);
    }

    static class DefaultMessageEntityNameFormatter implements MessageEntityNameFormatter {
        @Override
        public String formatEntityName(Class<?> messageType) {
            if (Proxy.isProxyClass(messageType) && messageType.getInterfaces().length > 0) {
                messageType = messageType.getInterfaces()[0];
            }
            return messageType.getPackageName() + ":" + messageType.getSimpleName();
        }
    }
}
