package com.myservicebus.logging;

import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;

public class ConsoleLoggerConfig {
    private volatile LogLevel minimumLevel = LogLevel.INFO;
    private final ConcurrentMap<String, LogLevel> categoryLevels = new ConcurrentHashMap<>();

    public ConsoleLoggerConfig setMinimumLevel(LogLevel level) {
        this.minimumLevel = level;
        return this;
    }

    public ConsoleLoggerConfig setLevel(String category, LogLevel level) {
        categoryLevels.put(category, level);
        return this;
    }

    public ConsoleLoggerConfig setLevel(Class<?> category, LogLevel level) {
        categoryLevels.put(category.getName(), level);
        return this;
    }

    public boolean isEnabled(String category, LogLevel level) {
        LogLevel configured = categoryLevels.getOrDefault(category, minimumLevel);
        return level.ordinal() >= configured.ordinal();
    }
}
