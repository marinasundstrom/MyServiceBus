package com.myservicebus;

public interface RetryObserver {
    void observe(RetryEvent retryEvent);
}
