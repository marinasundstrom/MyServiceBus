package com.myservicebus.logging;

public class Slf4jLoggerFactory implements LoggerFactory {
    @Override
    public Logger create(Class<?> type) {
        return new Slf4jLogger(org.slf4j.LoggerFactory.getLogger(type));
    }

    @Override
    public Logger create(String name) {
        return new Slf4jLogger(org.slf4j.LoggerFactory.getLogger(name));
    }
}
