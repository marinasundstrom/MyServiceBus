package com.myservicebus.logging;

import java.io.PrintStream;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;

class ConsoleLogger implements Logger {
    private final String name;
    private final ConsoleLoggerConfig config;
    private static final DateTimeFormatter FORMATTER = DateTimeFormatter.ISO_LOCAL_DATE_TIME;

    ConsoleLogger(String name, ConsoleLoggerConfig config) {
        this.name = name;
        this.config = config;
    }

    private String format(String message, Object... args) {
        if (args == null || args.length == 0) {
            return message;
        }
        // Replace slf4j style placeholders with String.format placeholders
        String fmt = message.replace("{}", "%s");
        return String.format(fmt, args);
    }

    private void log(LogLevel level, String message, Throwable exception, Object... args) {
        if (!config.isEnabled(name, level)) {
            return;
        }
        String formatted = format(message, args);
        String timestamp = LocalDateTime.now().format(FORMATTER);
        String output = String.format("%s [%s] %s", timestamp, level, formatted);
        PrintStream stream = (level.ordinal() >= LogLevel.ERROR.ordinal()) ? System.err : System.out;
        stream.println(output);
        if (exception != null) {
            exception.printStackTrace(stream);
        }
    }

    @Override
    public void debug(String message) {
        log(LogLevel.DEBUG, message, null);
    }

    @Override
    public void debug(String message, Object... args) {
        log(LogLevel.DEBUG, message, null, args);
    }

    @Override
    public void info(String message) {
        log(LogLevel.INFO, message, null);
    }

    @Override
    public void info(String message, Object... args) {
        log(LogLevel.INFO, message, null, args);
    }

    @Override
    public void warn(String message) {
        log(LogLevel.WARN, message, null);
    }

    @Override
    public void warn(String message, Object... args) {
        log(LogLevel.WARN, message, null, args);
    }

    @Override
    public void error(String message) {
        log(LogLevel.ERROR, message, null);
    }

    @Override
    public void error(String message, Throwable exception) {
        log(LogLevel.ERROR, message, exception);
    }

    @Override
    public void error(String message, Throwable exception, Object... args) {
        log(LogLevel.ERROR, message, exception, args);
    }

    @Override
    public boolean isDebugEnabled() {
        return config.isEnabled(name, LogLevel.DEBUG);
    }
}
