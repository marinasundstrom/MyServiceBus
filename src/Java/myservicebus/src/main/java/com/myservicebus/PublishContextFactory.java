package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;

/**
 * Factory for creating PublishContext instances.
 */
public interface PublishContextFactory {
    PublishContext create(Object message, CancellationToken cancellationToken);
}
