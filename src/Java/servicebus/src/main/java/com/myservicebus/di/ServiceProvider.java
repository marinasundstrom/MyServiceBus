package com.myservicebus.di;

import com.google.inject.Injector;

public interface ServiceProvider {
    <T> T getService(Class<T> type);

    ServiceScope createScope();
}