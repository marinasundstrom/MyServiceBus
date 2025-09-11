package com.myservicebus;

import java.util.UUID;

public class ScheduledMessageHandle {
    private final UUID tokenId;

    public ScheduledMessageHandle(UUID tokenId) {
        this.tokenId = tokenId;
    }

    public UUID getTokenId() {
        return tokenId;
    }
}
