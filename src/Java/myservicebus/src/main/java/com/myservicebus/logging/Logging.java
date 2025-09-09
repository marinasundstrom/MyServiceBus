package com.myservicebus.logging;

import java.util.function.Consumer;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceCollectionDecorator;

public class Logging extends ServiceCollectionDecorator {
    public Logging(ServiceCollection inner) {
        super(inner);
    }

    public ServiceCollection addLogging(Consumer<LoggingBuilder> configure) {
        LoggingBuilderImpl builder = new LoggingBuilderImpl(inner);
        if (configure != null) {
            configure.accept(builder);
        }
        return inner;
    }
}

