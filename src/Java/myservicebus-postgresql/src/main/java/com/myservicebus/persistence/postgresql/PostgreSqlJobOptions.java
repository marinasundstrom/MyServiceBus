package com.myservicebus.persistence.postgresql;

import java.time.Duration;

public final class PostgreSqlJobOptions {
    private Duration pollInterval = Duration.ofSeconds(1);
    private Duration leaseDuration = Duration.ofSeconds(30);
    private Duration heartbeatInterval = Duration.ofSeconds(5);
    private int batchSize = 16;

    public Duration getPollInterval() {
        return pollInterval;
    }

    public PostgreSqlJobOptions setPollInterval(Duration value) {
        pollInterval = value;
        return this;
    }

    public Duration getLeaseDuration() {
        return leaseDuration;
    }

    public PostgreSqlJobOptions setLeaseDuration(Duration value) {
        leaseDuration = value;
        return this;
    }

    public Duration getHeartbeatInterval() {
        return heartbeatInterval;
    }

    public PostgreSqlJobOptions setHeartbeatInterval(Duration value) {
        heartbeatInterval = value;
        return this;
    }

    public int getBatchSize() {
        return batchSize;
    }

    public PostgreSqlJobOptions setBatchSize(int value) {
        batchSize = value;
        return this;
    }

    void validate() {
        if (pollInterval == null || pollInterval.isZero() || pollInterval.isNegative()) {
            throw new IllegalStateException("The PostgreSQL job poll interval must be positive");
        }
        if (leaseDuration == null || leaseDuration.isZero() || leaseDuration.isNegative()) {
            throw new IllegalStateException("The PostgreSQL job lease duration must be positive");
        }
        if (heartbeatInterval == null
                || heartbeatInterval.isZero()
                || heartbeatInterval.isNegative()
                || heartbeatInterval.compareTo(leaseDuration) >= 0) {
            throw new IllegalStateException(
                    "The PostgreSQL job heartbeat interval must be positive and shorter than the lease duration");
        }
        if (batchSize <= 0) {
            throw new IllegalStateException("The PostgreSQL job batch size must be positive");
        }
    }
}
