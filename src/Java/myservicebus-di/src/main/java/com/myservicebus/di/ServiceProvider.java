package com.myservicebus.di;

import java.util.Set;

public interface ServiceProvider {
    <T> T getService(Class<T> type);

    default <T> T getRequiredService(Class<T> type) {
        T service = getService(type);
        if (service == null) {
            throw new IllegalStateException("Required service of type " + type.getName() + " is not registered.");
        }
        return service;
    }

    <T> Set<T> getServices(Class<T> iface);

    ServiceScope createScope();
}