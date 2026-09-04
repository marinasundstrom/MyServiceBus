package com.myservicebus.core;

/** Configures shared outgoing-message state before dispatch. */
@FunctionalInterface
public interface OutgoingMessageContextCallback {
    void configure(OutgoingMessageContext context);
}
