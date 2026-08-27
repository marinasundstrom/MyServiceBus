package com.myservicebus;

import java.time.Instant;

public interface BusHookEvent {
    Instant occurredAtUtc();
}
