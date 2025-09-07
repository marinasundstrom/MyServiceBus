package com.myservicebus.rabbitmq;

import com.myservicebus.SendContext;
import com.myservicebus.SendContextFactory;
import com.myservicebus.tasks.CancellationToken;

/**
 * Factory creating RabbitMqSendContext instances.
 */
public class RabbitMqSendContextFactory implements SendContextFactory {
    @Override
    public SendContext create(Object message, CancellationToken cancellationToken) {
        return new RabbitMqSendContext(message, cancellationToken);
    }
}
