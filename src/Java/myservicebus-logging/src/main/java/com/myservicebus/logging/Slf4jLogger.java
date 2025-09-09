package com.myservicebus.logging;

import org.slf4j.Logger;

class Slf4jLogger implements com.myservicebus.logging.Logger {
    private final String name;
    private final Logger logger;
    private final Slf4jLoggerConfig config;

    Slf4jLogger(String name, Logger logger, Slf4jLoggerConfig config) {
        this.name = name;
        this.logger = logger;
        this.config = config;
    }

    private boolean enabled(LogLevel level) {
        return config.isEnabled(name, level);
    }

    @Override
    public void debug(String message) {
        if (enabled(LogLevel.DEBUG) && logger.isDebugEnabled()) {
            logger.debug(message);
        }
    }

    @Override
    public void debug(String message, Object... args) {
        if (enabled(LogLevel.DEBUG) && logger.isDebugEnabled()) {
            logger.debug(message, args);
        }
    }

    @Override
    public void info(String message) {
        if (enabled(LogLevel.INFO)) {
            logger.info(message);
        }
    }

    @Override
    public void info(String message, Object... args) {
        if (enabled(LogLevel.INFO)) {
            logger.info(message, args);
        }
    }

    @Override
    public void warn(String message) {
        if (enabled(LogLevel.WARN)) {
            logger.warn(message);
        }
    }

    @Override
    public void warn(String message, Object... args) {
        if (enabled(LogLevel.WARN)) {
            logger.warn(message, args);
        }
    }

    @Override
    public void error(String message) {
        if (enabled(LogLevel.ERROR)) {
            logger.error(message);
        }
    }

    @Override
    public void error(String message, Throwable exception) {
        if (enabled(LogLevel.ERROR)) {
            logger.error(message, exception);
        }
    }

    @Override
    public void error(String message, Throwable exception, Object... args) {
        if (enabled(LogLevel.ERROR)) {
            logger.error(message, exception, args);
        }
    }

    @Override
    public boolean isDebugEnabled() {
        return enabled(LogLevel.DEBUG) && logger.isDebugEnabled();
    }
}
