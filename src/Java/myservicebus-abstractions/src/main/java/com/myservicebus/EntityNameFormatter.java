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
        EntityName attr = messageType.getAnnotation(EntityName.class);
        if (attr != null)
            return attr.value();
        return formatter.formatEntityName(messageType);
    }

    static class DefaultMessageEntityNameFormatter implements MessageEntityNameFormatter {
        @Override
        public String formatEntityName(Class<?> messageType) {
            if (Proxy.isProxyClass(messageType) && messageType.getInterfaces().length > 0) {
                messageType = messageType.getInterfaces()[0];
            }
            return String.format("TestApp:%s", messageType.getSimpleName());
        }
    }
}
