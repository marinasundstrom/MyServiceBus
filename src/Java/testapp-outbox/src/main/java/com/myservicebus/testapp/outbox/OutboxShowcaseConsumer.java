package com.myservicebus.testapp.outbox;

import TestApp.OutboxShowcaseMessage;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CopyOnWriteArrayList;

public final class OutboxShowcaseConsumer implements Consumer<OutboxShowcaseMessage> {
    private static final CopyOnWriteArrayList<OutboxShowcaseMessage> RECEIVED = new CopyOnWriteArrayList<>();

    public static List<OutboxShowcaseMessage> received() {
        return List.copyOf(RECEIVED);
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<OutboxShowcaseMessage> context) {
        RECEIVED.add(context.getMessage());
        return CompletableFuture.completedFuture(null);
    }
}
