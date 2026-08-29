package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

public class ScheduledMessageHandle {
    private final UUID tokenId;
    private final Instant scheduledTime;

    public ScheduledMessageHandle(UUID tokenId) {
        this(tokenId, null);
    }

    public ScheduledMessageHandle(UUID tokenId, Instant scheduledTime) {
        this.tokenId = tokenId;
        this.scheduledTime = scheduledTime;
    }

    public UUID getTokenId() {
        return tokenId;
    }

    public Instant getScheduledTime() {
        return scheduledTime;
    }
}
