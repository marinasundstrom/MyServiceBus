package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.topology.ConsumerDefinitionModel;
import com.myservicebus.topology.ConsumerRegistration;

/**
 * Narrow registration boundary implemented by JVM language projections before
 * consumer topology is materialized.
 */
public interface ConsumerRegistrationConfigurator {
    ConsumerDefinitionModel addConsumerRegistration(ConsumerRegistration<?> registration);

    ServiceCollection getServiceCollection();
}
