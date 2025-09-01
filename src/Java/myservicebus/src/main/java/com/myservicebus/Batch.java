package com.myservicebus;

import java.util.ArrayList;
import java.util.Collection;
import java.util.Collections;

/**
 * Represents a collection of messages that should be delivered together as a
 * single message payload. The batch itself is serialized as a JSON array so
 * that the envelope {@code message} property contains the grouped messages
 * directly, matching MassTransit batch semantics.
 *
 * @param <T> the message type contained in the batch
 */
public class Batch<T> extends ArrayList<T> {

    /**
     * Creates an empty batch.
     */
    public Batch() {
        super();
    }

    /**
     * Creates a batch containing the specified messages.
     *
     * @param messages the messages to include in the batch
     */
    public Batch(Collection<? extends T> messages) {
        super(messages);
    }

    /**
     * Creates a batch containing the specified messages.
     *
     * @param messages the messages to include in the batch
     */
    @SafeVarargs
    public Batch(T... messages) {
        Collections.addAll(this, messages);
    }
}
