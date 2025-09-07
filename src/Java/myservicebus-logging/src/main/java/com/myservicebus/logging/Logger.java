package com.myservicebus.logging;

public interface Logger {
    void debug(String message);
    void debug(String message, Object... args);
    void info(String message);
    void info(String message, Object... args);
    void warn(String message);
    void warn(String message, Object... args);
    void error(String message);
    void error(String message, Throwable exception);
    void error(String message, Throwable exception, Object... args);
    boolean isDebugEnabled();
}
