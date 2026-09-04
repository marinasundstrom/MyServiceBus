package com.myservicebus;

/** Configures shared outgoing-message state before dispatch. */
@FunctionalInterface
public interface OutgoingMessageContextCallback {
    void configure(OutgoingMessageContext context);
}
