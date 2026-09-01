package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;

class InventoryReservationRequestedConsumer implements Consumer<InventoryReservationRequested> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<InventoryReservationRequested> context) throws Exception {
        return context.publish(
                new InventoryReserved(context.getMessage().getOrderId()),
                context.getCancellationToken());
    }
}
