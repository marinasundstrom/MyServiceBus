package com.myservicebus;

/**
 * Specialized filter for {@link SendContext} messages.
 *
 * @param <T> The message type.
 */
public interface SendFilter<T> extends Filter<SendContext> {
}

