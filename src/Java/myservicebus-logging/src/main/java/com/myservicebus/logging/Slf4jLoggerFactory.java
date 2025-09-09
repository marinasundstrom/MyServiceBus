package com.myservicebus.logging;

import com.google.inject.Inject;

public class Slf4jLoggerFactory implements LoggerFactory {
    private final Slf4jLoggerConfig config;

    public Slf4jLoggerFactory() {
        this(new Slf4jLoggerConfig());
    }

    @Inject
    public Slf4jLoggerFactory(Slf4jLoggerConfig config) {
        this.config = config;
    }

    @Override
    public Logger create(Class<?> type) {
        return new Slf4jLogger(type.getName(), org.slf4j.LoggerFactory.getLogger(type), config);
    }

    @Override
    public Logger create(String name) {
        return new Slf4jLogger(name, org.slf4j.LoggerFactory.getLogger(name), config);
    }
}
