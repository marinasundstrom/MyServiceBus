package com.myservicebus.logging;

import java.util.function.Consumer;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceDescriptor;
import com.myservicebus.di.ServiceLifetime;

class LoggingBuilderImpl implements LoggingBuilder {
    private final ServiceCollection services;
    private LoggerFactory factory;

    LoggingBuilderImpl(ServiceCollection services) {
        this.services = services;
    }

    LoggingBuilderImpl() {
        this.services = null;
    }

    @Override
    public void addConsole() {
        addConsole(cfg -> {
        });
    }

    @Override
    public void addConsole(Consumer<ConsoleLoggerConfig> configure) {
        ConsoleLoggerConfig config = new ConsoleLoggerConfig();
        if (configure != null) {
            configure.accept(config);
        }

        if (services != null) {
            services.add(new ServiceDescriptor(ConsoleLoggerConfig.class, null, null, config,
                    ServiceLifetime.SINGLETON, false));
            services.addSingleton(LoggerFactory.class, ConsoleLoggerFactory.class);
        } else {
            factory = new ConsoleLoggerFactory(config);
        }
    }

    @Override
    public void addSlf4j() {
        addSlf4j(cfg -> {
        });
    }

    @Override
    public void addSlf4j(Consumer<Slf4jLoggerConfig> configure) {
        Slf4jLoggerConfig config = new Slf4jLoggerConfig();
        if (configure != null) {
            configure.accept(config);
        }

        if (services != null) {
            services.add(new ServiceDescriptor(Slf4jLoggerConfig.class, null, null, config,
                    ServiceLifetime.SINGLETON, false));
            services.addSingleton(LoggerFactory.class, Slf4jLoggerFactory.class);
        } else {
            factory = new Slf4jLoggerFactory(config);
        }
    }

    LoggerFactory getFactory() {
        if (factory == null) {
            factory = new ConsoleLoggerFactory(new ConsoleLoggerConfig());
        }
        return factory;
    }
}
