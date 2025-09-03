package com.myservicebus.di;

import com.google.inject.Provider;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class Slf4jLoggerProvider implements Provider<Logger> {
    @Override
    public Logger get() {
        return LoggerFactory.getLogger("MyServiceBus");
    }
}
