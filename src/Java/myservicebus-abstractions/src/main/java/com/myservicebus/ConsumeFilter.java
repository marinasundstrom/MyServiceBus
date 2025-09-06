package com.myservicebus;

/**
 * Specialized filter for {@link ConsumeContext} messages.
 */
public interface ConsumeFilter<T> extends Filter<ConsumeContext<T>> {
}

