package com.myservicebus.logging;

import java.util.function.Consumer;

public final class LoggerFactoryBuilder {
    private LoggerFactoryBuilder() {
    }

    public static LoggerFactory create(Consumer<LoggingBuilder> configure) {
        LoggingBuilderImpl builder = new LoggingBuilderImpl();
        if (configure != null) {
            configure.accept(builder);
        }
        return builder.getFactory();
    }
}

