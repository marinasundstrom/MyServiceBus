package com.myservicebus.logging;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

public class LoggingBuilderTest {
    @Test
    void addConsoleRegistersConsoleLoggerFactory() {
        ServiceCollection services = new ServiceCollection();
        services.from(Logging.class).addLogging(b -> b.addConsole());
        ServiceProvider provider = services.buildServiceProvider();
        LoggerFactory factory = provider.getService(LoggerFactory.class);
        assertTrue(factory.create("test") instanceof ConsoleLogger);
    }

    @Test
    void addSlf4jRegistersSlf4jLoggerFactory() {
        ServiceCollection services = new ServiceCollection();
        services.from(Logging.class).addLogging(b -> b.addSlf4j());
        ServiceProvider provider = services.buildServiceProvider();
        LoggerFactory factory = provider.getService(LoggerFactory.class);
        assertTrue(factory.create("test") instanceof Slf4jLogger);
    }

    @Test
    void configureSlf4jSetsMinimumLevel() {
        ServiceCollection services = new ServiceCollection();
        services.from(Logging.class).addLogging(b -> b.addSlf4j(cfg -> cfg.setMinimumLevel(LogLevel.ERROR)));
        ServiceProvider provider = services.buildServiceProvider();
        LoggerFactory factory = provider.getService(LoggerFactory.class);
        Logger logger = factory.create("test");
        assertFalse(logger.isDebugEnabled());
    }

    @Test
    void loggerFactoryBuilderCreatesConsoleFactory() {
        LoggerFactory factory = LoggerFactoryBuilder.create(b -> b.addConsole());
        assertTrue(factory.create("test") instanceof ConsoleLogger);
    }

    @Test
    void loggerFactoryBuilderDefaultsToConsole() {
        LoggerFactory factory = LoggerFactoryBuilder.create(b -> {
        });
        assertTrue(factory.create("test") instanceof ConsoleLogger);
    }

    @Test
    void loggerFactoryBuilderCreatesSlf4jFactoryWithConfig() {
        LoggerFactory factory = LoggerFactoryBuilder
                .create(b -> b.addSlf4j(cfg -> cfg.setMinimumLevel(LogLevel.ERROR)));
        Logger logger = factory.create("test");
        assertFalse(logger.isDebugEnabled());
    }
}
