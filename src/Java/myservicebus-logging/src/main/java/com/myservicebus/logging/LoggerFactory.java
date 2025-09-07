package com.myservicebus.logging;

public interface LoggerFactory {
    Logger create(Class<?> type);
    Logger create(String name);
}
