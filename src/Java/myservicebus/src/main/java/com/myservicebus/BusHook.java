package com.myservicebus;

public interface BusHook {
    void handle(BusHookEvent busEvent);
}
