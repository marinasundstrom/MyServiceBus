package com.myservicebus;

public interface RequestClientFactory {
    <TRequest> RequestClient<TRequest> create(Class<TRequest> requestType);
}
