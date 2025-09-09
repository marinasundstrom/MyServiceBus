package com.myservicebus.logging;

import java.util.function.Consumer;

import com.myservicebus.di.ServiceCollection;

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
        if (services != null) {
            services.addConsoleLogger(configure);
        } else {
            ConsoleLoggerConfig config = new ConsoleLoggerConfig();
            if (configure != null) {
                configure.accept(config);
            }
            factory = new ConsoleLoggerFactory(config);
        }
    }

    @Override
    public void addSlf4j() {
        if (services != null) {
            services.addSlf4jLogger();
        } else {
            factory = new Slf4jLoggerFactory();
        }
    }

    LoggerFactory getFactory() {
        if (factory == null) {
            factory = new Slf4jLoggerFactory();
        }
        return factory;
    }
}

