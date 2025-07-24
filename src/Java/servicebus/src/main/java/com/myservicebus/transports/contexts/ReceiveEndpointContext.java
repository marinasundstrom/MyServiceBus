package com.myservicebus.transports.contexts;

import com.myservicebus.contexts.PipeContext;
import com.myservicebus.middleware.ReceivePipe;

public interface ReceiveEndpointContext extends PipeContext {
    ReceivePipe getReceivePipe();

    // PublishTopology getPublish();

    // PublishEndpointProvider getPublishEndpointProvider();

    // SendEndpointProvider getSendEndpointProvider();
}