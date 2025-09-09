package com.myservicebus.logging;

import java.util.function.Consumer;

public interface LoggingBuilder {
    void addConsole();
    void addConsole(Consumer<ConsoleLoggerConfig> configure);
    void addSlf4j();
}

