package com.myservicebus;

import com.myservicebus.core.OutgoingMessagePublisher;
import com.myservicebus.core.OutgoingMessagePublisherProvider;

public interface PublishEndpointProvider extends OutgoingMessagePublisherProvider {
    PublishEndpoint getPublishEndpoint();

    @Override
    default OutgoingMessagePublisher getMessagePublisher() {
        return getPublishEndpoint();
    }
}
