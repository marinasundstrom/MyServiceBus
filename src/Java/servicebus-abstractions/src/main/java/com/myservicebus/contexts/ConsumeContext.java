package com.myservicebus.contexts;

import com.myservicebus.MessageConsumeContext;
import com.myservicebus.tasks.CancellationToken;

import transports.PublishEndpoint;
import transports.SendEndpoint;

public interface ConsumeContext<T>
        extends PipeContext, MessageConsumeContext, PublishEndpoint, SendEndpoint, SendEndpointProvider {

    T getMessage();

    CancellationToken cancellationToken();
}
