package com.myservicebus.di;

import com.google.inject.Provider;

@FunctionalInterface
public interface ServiceProviderBasedProvider<T> {
    Provider<T> create(ServiceProvider serviceProvider);
}