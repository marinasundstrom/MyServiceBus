package com.myservicebus;

public interface PublishEndpointProvider extends OutgoingMessagePublisherProvider {
    PublishEndpoint getPublishEndpoint();

    @Override
    default OutgoingMessagePublisher getMessagePublisher() {
        return getPublishEndpoint();
    }
}
