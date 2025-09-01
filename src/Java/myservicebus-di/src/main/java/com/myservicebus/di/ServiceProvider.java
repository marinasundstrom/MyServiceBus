package com.myservicebus.di;

import java.util.Set;

public interface ServiceProvider {
    <T> T getService(Class<T> type);

    <T> Set<T> getServices(Class<T> iface);

    ServiceScope createScope();
}