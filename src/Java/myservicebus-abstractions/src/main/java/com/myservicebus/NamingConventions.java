package com.myservicebus;

import java.lang.reflect.Proxy;

public class NamingConventions {

    private static MessageEntityNameFormatter entityNameFormatter = new DefaultMessageEntityNameFormatter();

    public static MessageEntityNameFormatter getEntityNameFormatter() {
        return entityNameFormatter;
    }

    public static void setEntityNameFormatter(MessageEntityNameFormatter formatter) {
        entityNameFormatter = formatter;
    }

    public static String getMessageUrn(Class<?> messageType) {
        return String.format("urn:message:%s", getMessageName(messageType));
    }

    public static String getExchangeName(Class<?> messageType) {
        EntityName attr = messageType.getAnnotation(EntityName.class);
        if (attr != null)
            return attr.value();
        return entityNameFormatter.formatEntityName(messageType);
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

    static class DefaultMessageEntityNameFormatter implements MessageEntityNameFormatter {
        @Override
        public String formatEntityName(Class<?> messageType) {
            return getMessageName(messageType);
        }
    }
}
