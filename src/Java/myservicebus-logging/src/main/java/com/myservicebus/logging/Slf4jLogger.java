package com.myservicebus.logging;

import org.slf4j.Logger;

class Slf4jLogger implements com.myservicebus.logging.Logger {
    private final Logger logger;

    Slf4jLogger(Logger logger) {
        this.logger = logger;
    }

    @Override
    public void debug(String message) {
        logger.debug(message);
    }

    @Override
    public void debug(String message, Object... args) {
        logger.debug(message, args);
    }

    @Override
    public void info(String message) {
        logger.info(message);
    }

    @Override
    public void info(String message, Object... args) {
        logger.info(message, args);
    }

    @Override
    public void warn(String message) {
        logger.warn(message);
    }

    @Override
    public void warn(String message, Object... args) {
        logger.warn(message, args);
    }

    @Override
    public void error(String message) {
        logger.error(message);
    }

    @Override
    public void error(String message, Throwable exception) {
        logger.error(message, exception);
    }

    @Override
    public void error(String message, Throwable exception, Object... args) {
        logger.error(message, exception, args);
    }

    @Override
    public boolean isDebugEnabled() {
        return logger.isDebugEnabled();
    }
}
