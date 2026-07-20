package com.myservicebus;

@FunctionalInterface
public interface PublishAddressProvider {
    String getPublishAddress(String entityName);
}
