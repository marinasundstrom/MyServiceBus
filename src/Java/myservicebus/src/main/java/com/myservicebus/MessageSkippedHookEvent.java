package com.myservicebus;

import java.time.Instant;
import java.util.List;

/** A message preserved in the skipped queue because no advertised contract was registered. */
public record MessageSkippedHookEvent(Instant occurredAtUtc, String endpointName, String messageId,
        List<String> advertisedMessageUrns) implements BusHookEvent {
    public MessageSkippedHookEvent { advertisedMessageUrns = List.copyOf(advertisedMessageUrns); }
}
