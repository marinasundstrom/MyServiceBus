package com.myservicebus.di;

import java.util.function.Supplier;

@FunctionalInterface
public interface ServiceProviderBasedProvider<T> {
    Supplier<? extends T> create(ServiceProvider serviceProvider);
}
