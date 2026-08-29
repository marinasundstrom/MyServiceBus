package com.myservicebus.persistence;

public record OutboxDispatchBatchResult(
        int leased,
        int dispatched,
        int failed,
        int lostLeases) {
}
