package com.myservicebus.azure.servicebus;

import com.myservicebus.MessageEntityNameFormatterSpecific;

import java.util.Map;

public final class AzureServiceBusMessageConfigurator<T> {
    private final Class<T> messageType;
    private final Map<Class<?>, String> entityNames;

    AzureServiceBusMessageConfigurator(Class<T> messageType, Map<Class<?>, String> entityNames) {
        this.messageType = messageType;
        this.entityNames = entityNames;
    }

    public void setEntityName(String name) {
        if (name == null || name.isBlank()) {
            throw new IllegalArgumentException("Azure Service Bus entity name cannot be blank");
        }
        entityNames.put(messageType, name);
    }

    public void setEntityNameFormatter(MessageEntityNameFormatterSpecific<T> formatter) {
        entityNames.put(messageType, formatter.formatEntityName());
    }
}
