package com.myservicebus.azure.servicebus;

import com.myservicebus.EntityName;
import com.myservicebus.MessageEntityNameFormatter;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/** Formats message entity names using the MassTransit Azure Service Bus convention. */
public final class AzureServiceBusMessageEntityNameFormatter implements MessageEntityNameFormatter {
    public static final AzureServiceBusMessageEntityNameFormatter INSTANCE =
            new AzureServiceBusMessageEntityNameFormatter();

    private final Map<Class<?>, String> cache = new ConcurrentHashMap<>();

    private AzureServiceBusMessageEntityNameFormatter() { }

    @Override
    public String formatEntityName(Class<?> messageType) {
        if (messageType == null) {
            throw new IllegalArgumentException("Message type cannot be null");
        }
        EntityName annotation = messageType.getAnnotation(EntityName.class);
        return annotation != null
                ? annotation.value()
                : cache.computeIfAbsent(messageType, AzureServiceBusMessageEntityNameFormatter::format);
    }

    private static String format(Class<?> messageType) {
        if (messageType.isArray()) {
            return format(messageType.getComponentType()) + "__";
        }

        StringBuilder builder = new StringBuilder();
        Package messagePackage = messageType.getPackage();
        if (messagePackage != null && !messagePackage.getName().isBlank()) {
            builder.append(messagePackage.getName()).append('/');
        }
        appendTypeName(builder, messageType);
        return builder.toString();
    }

    private static void appendTypeName(StringBuilder builder, Class<?> messageType) {
        Class<?> enclosingType = messageType.getEnclosingClass();
        if (enclosingType != null) {
            appendTypeName(builder, enclosingType);
            builder.append('-');
        }
        builder.append(messageType.getSimpleName());
    }
}
