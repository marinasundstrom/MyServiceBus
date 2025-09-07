package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;

/**
 * Default publish context factory creating standard PublishContext instances.
 */
public class DefaultPublishContextFactory implements PublishContextFactory {
    @Override
    public PublishContext create(Object message, CancellationToken cancellationToken) {
        return new PublishContext(message, cancellationToken);
    }
}
