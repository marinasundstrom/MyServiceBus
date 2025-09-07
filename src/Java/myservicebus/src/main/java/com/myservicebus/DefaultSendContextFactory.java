package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;

/**
 * Default send context factory creating standard SendContext instances.
 */
public class DefaultSendContextFactory implements SendContextFactory {
    @Override
    public SendContext create(Object message, CancellationToken cancellationToken) {
        return new SendContext(message, cancellationToken);
    }
}
