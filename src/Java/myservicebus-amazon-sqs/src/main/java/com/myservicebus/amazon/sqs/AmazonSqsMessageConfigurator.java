package com.myservicebus.amazon.sqs;

import com.myservicebus.MessageEntityNameFormatterSpecific;
import java.util.Map;

public final class AmazonSqsMessageConfigurator<T> {
    private final Class<T> messageType;
    private final Map<Class<?>, String> entityNames;

    AmazonSqsMessageConfigurator(Class<T> messageType, Map<Class<?>, String> entityNames) {
        this.messageType = messageType;
        this.entityNames = entityNames;
    }

    public void setEntityName(String name) {
        AmazonSqsEntityNames.validate(name);
        entityNames.put(messageType, name);
    }

    public void setEntityNameFormatter(MessageEntityNameFormatterSpecific<T> formatter) {
        setEntityName(formatter.formatEntityName());
    }
}
