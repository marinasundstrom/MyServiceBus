package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.serialization.MessageIntent;

/**
 * Specialized context used for publish operations.
 */
public class PublishContext extends SendContext {
    public PublishContext(Object message) {
        super(message);
        setIntent(MessageIntent.PUBLISH);
    }

    public PublishContext(Object message, CancellationToken cancellationToken) {
        super(message, cancellationToken);
        setIntent(MessageIntent.PUBLISH);
    }
}
