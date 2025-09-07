package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;

/**
 * Factory for creating SendContext instances.
 */
public interface SendContextFactory {
    SendContext create(Object message, CancellationToken cancellationToken);
}
