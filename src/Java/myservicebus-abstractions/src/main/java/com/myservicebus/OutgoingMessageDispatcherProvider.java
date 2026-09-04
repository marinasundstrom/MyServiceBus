package com.myservicebus;

/** Shared JVM capability for resolving a destination-bound dispatcher. */
public interface OutgoingMessageDispatcherProvider {
    OutgoingMessageDispatcher getMessageDispatcher(String destination);
}
