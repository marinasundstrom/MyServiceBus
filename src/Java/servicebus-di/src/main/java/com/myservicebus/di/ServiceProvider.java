package com.myservicebus.di;

public interface ServiceProvider {
    <T> T getService(Class<T> type);

    ServiceScope createScope();
}