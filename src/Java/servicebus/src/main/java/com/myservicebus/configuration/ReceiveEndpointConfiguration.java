package com.myservicebus.configuration;

import java.net.URI;
import java.util.Optional;

import com.myservicebus.contexts.ReceiveEndpointContext;
import com.myservicebus.middleware.ConsumePipe;
import com.myservicebus.middleware.ReceivePipe;

public interface ReceiveEndpointConfiguration {

    ConsumePipe getConsumePipe();

    URI getHostAddress();

    URI getInputAddress();

    boolean getConfigureConsumeTopology();

    boolean getPublishFaults();

    int getPrefetchCount();

    Optional<Integer> getConcurrentMessageLimit();

    ReceivePipe createReceivePipe();

    ReceiveEndpointContext createReceiveEndpointContext();
}
