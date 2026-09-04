package com.myservicebus.core;

/** Shared JVM capability for resolving a destination-bound dispatcher. */
public interface OutgoingMessageDispatcherProvider {
    OutgoingMessageDispatcher getMessageDispatcher(String destination);
}
