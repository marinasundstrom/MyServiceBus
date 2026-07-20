package com.myservicebus.topology;

public record ReceiveEndpointDefinition(
        String name,
        boolean durable,
        boolean temporary) {
}
