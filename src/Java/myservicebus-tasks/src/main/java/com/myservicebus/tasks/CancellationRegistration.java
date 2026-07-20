package com.myservicebus.tasks;

@FunctionalInterface
public interface CancellationRegistration extends AutoCloseable {
    @Override
    void close();
}
