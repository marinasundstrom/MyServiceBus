package com.myservicebus;

import java.time.Instant;

public record BusLifecycleHookEvent(
        Instant occurredAtUtc,
        String state,
        String busAddress) implements BusHookEvent {
}
