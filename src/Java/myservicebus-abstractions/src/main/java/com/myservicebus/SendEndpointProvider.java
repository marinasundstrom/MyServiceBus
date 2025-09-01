package com.myservicebus;

public interface SendEndpointProvider {
    SendEndpoint getSendEndpoint(String uri);
}