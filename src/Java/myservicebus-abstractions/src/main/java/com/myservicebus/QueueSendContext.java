package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;

public class QueueSendContext extends SendContext {
    private String routingKey;

    public QueueSendContext(Object message, CancellationToken cancellationToken) {
        super(message, cancellationToken);
    }

    public String getRoutingKey() {
        return routingKey;
    }

    public void setRoutingKey(String routingKey) {
        this.routingKey = routingKey;
    }
}

