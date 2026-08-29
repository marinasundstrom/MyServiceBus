package com.myservicebus.persistence;

public enum OutboxDeliveryIntent {
    SEND,
    PUBLISH,
    REPLY,
    FAULT
}
