package com.myservicebus.contexts;

import transports.SendEndpoint;

public interface SendEndpointProvider {
    SendEndpoint getSendEndpoint(String uri);
}