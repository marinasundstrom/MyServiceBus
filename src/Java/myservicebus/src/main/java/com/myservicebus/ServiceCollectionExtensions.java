package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import java.util.function.Consumer;

public class ServiceCollectionExtensions {
    public static ServiceCollection addServiceBus(ServiceCollection thiz,
            Consumer<BusRegistrationConfigurator> configure) {

        /*
         * var configurator = new BusRegistrationConfigurator(services);
         * configure(configurator); // <-- you call AddConsumer, AddSaga, etc.
         * //configurator.SetBusFactory(...); // set up transport like RabbitMQ
         * 
         * // Registers bus and hosted service
         * //configurator.CompleteRegistration();
         * 
         * services.AddHostedService<ServiceBusHostedService>();
         * 
         */

        return thiz;
    }
}
