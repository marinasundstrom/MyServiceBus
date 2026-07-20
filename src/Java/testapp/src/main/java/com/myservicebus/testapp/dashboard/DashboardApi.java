package com.myservicebus.testapp.dashboard;

import com.myservicebus.inspection.BusInspectionProvider;
import io.javalin.Javalin;

public final class DashboardApi {
    private DashboardApi() {
    }

    public static void register(Javalin app, BusInspectionProvider inspectionProvider, DashboardMetadata metadata, DashboardState state) {
        app.get("/inspection/v1/overview", ctx -> ctx.json(DashboardSnapshotFactory.createOverview(inspectionProvider, metadata, state)));
        app.get("/inspection/v1/messages", ctx -> ctx.json(DashboardSnapshotFactory.createMessages(inspectionProvider, metadata)));
        app.get("/inspection/v1/consumers", ctx -> ctx.json(DashboardSnapshotFactory.createConsumers(inspectionProvider, metadata)));
        app.get("/inspection/v1/topology", ctx -> ctx.json(DashboardSnapshotFactory.createTopology(inspectionProvider, metadata)));
        app.get("/inspection/v1/queues", ctx -> ctx.json(DashboardSnapshotFactory.createQueues(inspectionProvider, metadata)));
        app.get("/inspection/v1/metrics", ctx -> ctx.json(DashboardSnapshotFactory.createMetrics(inspectionProvider, metadata, state)));
    }
}
