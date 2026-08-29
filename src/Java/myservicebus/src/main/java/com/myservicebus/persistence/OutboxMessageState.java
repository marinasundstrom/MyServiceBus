package com.myservicebus.persistence;

public enum OutboxMessageState {
    PENDING,
    LEASED,
    DISPATCHED,
    DEAD
}
