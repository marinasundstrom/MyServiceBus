package com.myservicebus;

public interface BusRegistrationConfigurator {
    <T> void addConsumer(Class<T> consumerClass);
}