package com.myservicebus.persistence.postgresql;

import java.time.Instant;

public record PostgreSqlOutboxBacklog(
        String serviceName,
        int pending,
        int leased,
        int retrying,
        int dispatched,
        int dead,
        int cancelled,
        Instant oldestUndispatchedAtUtc) {
}
