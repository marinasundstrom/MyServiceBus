package com.myservicebus.abstractions;

public interface SendEndpointProvider {
    SendEndpoint getSendEndpoint(String uri);
}