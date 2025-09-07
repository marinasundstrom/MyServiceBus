package com.myservicebus.rabbitmq;

import com.myservicebus.PublishContext;
import com.myservicebus.PublishContextFactory;
import com.myservicebus.tasks.CancellationToken;

/**
 * Factory creating RabbitMqPublishContext instances.
 */
public class RabbitMqPublishContextFactory implements PublishContextFactory {
    @Override
    public PublishContext create(Object message, CancellationToken cancellationToken) {
        return new RabbitMqPublishContext(message, cancellationToken);
    }
}
