package com.myservicebus;

@FunctionalInterface
public interface PublishAddressProvider {
    String getPublishAddress(String entityName);

    default String getPublishEntityName(Class<?> messageType) {
        return EntityNameFormatter.format(messageType);
    }

    default String getPublishAddress(Class<?> messageType) {
        return getPublishAddress(getPublishEntityName(messageType));
    }
}
