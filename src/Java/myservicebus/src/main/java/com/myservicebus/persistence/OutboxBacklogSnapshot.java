package com.myservicebus.persistence;

import java.time.Instant;

public record OutboxBacklogSnapshot(
        int pending,
        int leased,
        int retrying,
        int dispatched,
        int dead,
        int cancelled,
        Instant oldestUndispatchedAtUtc) {
}
