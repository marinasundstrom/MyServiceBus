package com.myservicebus.testapp.dashboard;

import com.myservicebus.MessageBus;
import io.javalin.Javalin;

public final class DashboardApi {
    private DashboardApi() {
    }

    public static void register(Javalin app, MessageBus bus, DashboardMetadata metadata, DashboardState state) {
        app.get("/inspection/v1/overview", ctx -> ctx.json(DashboardSnapshotFactory.createOverview(bus, metadata, state)));
        app.get("/inspection/v1/messages", ctx -> ctx.json(DashboardSnapshotFactory.createMessages(bus, metadata)));
        app.get("/inspection/v1/consumers", ctx -> ctx.json(DashboardSnapshotFactory.createConsumers(bus, metadata)));
        app.get("/inspection/v1/topology", ctx -> ctx.json(DashboardSnapshotFactory.createTopology(bus, metadata)));
        app.get("/inspection/v1/queues", ctx -> ctx.json(DashboardSnapshotFactory.createQueues(bus, metadata)));
        app.get("/inspection/v1/metrics", ctx -> ctx.json(DashboardSnapshotFactory.createMetrics(bus, metadata, state)));
    }
}
