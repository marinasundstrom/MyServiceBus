package com.myservicebus.persistence.postgresql;

import com.myservicebus.PublishEndpoint;
import com.myservicebus.ScheduleCancellationResult;
import com.myservicebus.ScheduleMessageProvider;
import com.myservicebus.SchedulingDurability;
import com.myservicebus.ScheduledMessageHandle;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.persistence.OutboxSession;
import com.myservicebus.tasks.CancellationToken;
import java.time.Clock;
import java.time.Instant;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import javax.sql.DataSource;

/** Persists scheduled delivery intent in the current PostgreSQL outbox transaction. */
public final class PostgreSqlScheduleMessageProvider implements ScheduleMessageProvider {
    private final OutboxSession session;
    private final PublishEndpoint publishEndpoint;
    private final SendEndpointProvider sendEndpointProvider;
    private final PostgreSqlOutboxStore store;
    private final Clock clock;

    public PostgreSqlScheduleMessageProvider(
            DataSource dataSource,
            String serviceName,
            OutboxSession session,
            PublishEndpoint publishEndpoint,
            SendEndpointProvider sendEndpointProvider) {
        this(dataSource, serviceName, session, publishEndpoint, sendEndpointProvider, Clock.systemUTC());
    }

    public PostgreSqlScheduleMessageProvider(
            DataSource dataSource,
            String serviceName,
            OutboxSession session,
            PublishEndpoint publishEndpoint,
            SendEndpointProvider sendEndpointProvider,
            Clock clock) {
        this.session = Objects.requireNonNull(session, "session");
        this.publishEndpoint = Objects.requireNonNull(publishEndpoint, "publishEndpoint");
        this.sendEndpointProvider = Objects.requireNonNull(sendEndpointProvider, "sendEndpointProvider");
        this.store = new PostgreSqlOutboxStore(dataSource, serviceName);
        this.clock = Objects.requireNonNull(clock, "clock");
    }

    @Override
    public SchedulingDurability getDurability() {
        return SchedulingDurability.DURABLE;
    }

    @Override
    public boolean supportsCancellation() {
        return true;
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> schedulePublish(
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken) {
        ensureActiveTransaction();
        UUID tokenId = UUID.randomUUID();
        return publishEndpoint.publish(message, context -> {
            context.setMessageId(tokenId);
            context.setScheduledEnqueueTime(scheduledTime);
        }, cancellationToken).thenApply(ignored -> new ScheduledMessageHandle(tokenId, scheduledTime));
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> scheduleSend(
            String destinationAddress,
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken) {
        ensureActiveTransaction();
        UUID tokenId = UUID.randomUUID();
        SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint(destinationAddress);
        return endpoint.send(message, context -> {
            context.setMessageId(tokenId);
            context.setScheduledEnqueueTime(scheduledTime);
        }, cancellationToken).thenApply(ignored -> new ScheduledMessageHandle(tokenId, scheduledTime));
    }

    @Override
    public CompletionStage<ScheduleCancellationResult> cancel(
            UUID tokenId,
            CancellationToken cancellationToken) {
        if (cancellationToken.isCancelled()) {
            return CompletableFuture.failedFuture(new java.util.concurrent.CancellationException());
        }
        return store.cancelScheduled(tokenId, clock.instant());
    }

    private void ensureActiveTransaction() {
        if (session.getWriter() == null) {
            throw new IllegalStateException(
                    "Durable scheduling requires an active outbox transaction in the current service scope.");
        }
    }
}
