package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.CompletableFuture;

public interface MessageScheduler {
    <T> CompletableFuture<Void> schedulePublish(T message, Instant scheduledTime);
    default <T> CompletableFuture<Void> schedulePublish(T message, Duration delay) {
        return schedulePublish(message, Instant.now().plus(delay));
    }
    <T> CompletableFuture<Void> scheduleSend(String destination, T message, Instant scheduledTime);
    default <T> CompletableFuture<Void> scheduleSend(String destination, T message, Duration delay) {
        return scheduleSend(destination, message, Instant.now().plus(delay));
    }
}
