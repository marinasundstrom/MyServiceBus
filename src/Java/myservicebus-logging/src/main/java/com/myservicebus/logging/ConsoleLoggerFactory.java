package com.myservicebus.logging;

import com.google.inject.Inject;

public class ConsoleLoggerFactory implements LoggerFactory {
    private final ConsoleLoggerConfig config;

    @Inject
    public ConsoleLoggerFactory(ConsoleLoggerConfig config) {
        this.config = config;
    }

    @Override
    public Logger create(Class<?> type) {
        return new ConsoleLogger(type.getName(), config);
    }

    @Override
    public Logger create(String name) {
        return new ConsoleLogger(name, config);
    }
}
