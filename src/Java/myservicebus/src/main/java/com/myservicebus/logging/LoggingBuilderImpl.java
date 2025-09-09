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
        addSlf4j(cfg -> {
        });
    }

    @Override
    public void addSlf4j(Consumer<Slf4jLoggerConfig> configure) {
        if (services != null) {
            services.addSlf4jLogger(configure);
        } else {
            Slf4jLoggerConfig config = new Slf4jLoggerConfig();
            if (configure != null) {
                configure.accept(config);
            }
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
