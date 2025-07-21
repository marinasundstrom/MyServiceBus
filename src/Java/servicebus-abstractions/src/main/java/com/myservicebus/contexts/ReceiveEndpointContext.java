package com.myservicebus.contexts;

import com.myservicebus.middleware.ReceivePipe;

public interface ReceiveEndpointContext extends PipeContext {
    ReceivePipe getReceivePipe();

    // PublishTopology getPublish();

    // PublishEndpointProvider getPublishEndpointProvider();

    // SendEndpointProvider getSendEndpointProvider();
}