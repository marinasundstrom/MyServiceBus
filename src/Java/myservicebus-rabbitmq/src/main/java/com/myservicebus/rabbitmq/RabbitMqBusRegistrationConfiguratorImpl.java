package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.di.ServiceCollection;
import java.util.function.Consumer;

class RabbitMqBusRegistrationConfiguratorImpl extends BusRegistrationConfiguratorImpl
        implements RabbitMqBusRegistrationConfigurator {

    private String host = "localhost";
    private String username = "guest";
    private String password = "guest";

    public RabbitMqBusRegistrationConfiguratorImpl(ServiceCollection services) {
        super(services);
    }

    @Override
    public void host(String host, Consumer<RabbitMqHostConfigurator> configure) {
        this.host = host;
        if (configure != null) {
            RabbitMqHostConfiguratorImpl cfg = new RabbitMqHostConfiguratorImpl();
            configure.accept(cfg);
            this.username = cfg.username;
            this.password = cfg.password;
        }
    }

    public String getHost() {
        return host;
    }

    public String getUsername() {
        return username;
    }

    public String getPassword() {
        return password;
    }

    private static class RabbitMqHostConfiguratorImpl implements RabbitMqHostConfigurator {
        private String username = "guest";
        private String password = "guest";

        @Override
        public void username(String username) {
            this.username = username;
        }

        @Override
        public void password(String password) {
            this.password = password;
        }
    }
}

