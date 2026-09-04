package com.myservicebus;

/** Shared JVM capability for resolving the current message publisher. */
public interface OutgoingMessagePublisherProvider {
    OutgoingMessagePublisher getMessagePublisher();
}
