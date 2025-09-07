package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;

/**
 * Specialized context used for publish operations.
 */
public class PublishContext extends SendContext {
    public PublishContext(Object message, CancellationToken cancellationToken) {
        super(message, cancellationToken);
    }
}
