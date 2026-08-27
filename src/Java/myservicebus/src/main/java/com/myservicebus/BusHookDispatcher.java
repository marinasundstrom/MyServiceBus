package com.myservicebus;

import java.util.Set;

import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

final class BusHookDispatcher {
    private final Set<BusHook> hooks;
    private final Logger logger;

    BusHookDispatcher(Set<BusHook> hooks, LoggerFactory loggerFactory) {
        this.hooks = hooks;
        this.logger = loggerFactory == null ? null : loggerFactory.create(BusHookDispatcher.class);
    }

    void dispatch(BusHookEvent busEvent) {
        for (BusHook hook : hooks) {
            try {
                hook.handle(busEvent);
            } catch (RuntimeException exception) {
                if (logger != null) {
                    logger.warn("MyServiceBus hook failed: " + hook.getClass().getName(), exception);
                }
            }
        }
    }

    boolean isEnabled() {
        return !hooks.isEmpty();
    }
}
