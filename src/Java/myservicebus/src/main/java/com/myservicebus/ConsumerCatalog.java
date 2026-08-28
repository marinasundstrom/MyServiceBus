package com.myservicebus;

/**
 * Registers a finite set of consumers with a bus configurator.
 */
@FunctionalInterface
public interface ConsumerCatalog {
    void register(BusRegistrationConfigurator configurator);
}
