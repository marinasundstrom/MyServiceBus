package com.myservicebus.monitoring;

import javax.inject.Inject;

import com.myservicebus.ScheduledWorkObserver;
import com.myservicebus.ScheduledWorkState;

final class MonitoringScheduledWorkObserver implements ScheduledWorkObserver {
    private final MonitoringExporter exporter;

    @Inject
    MonitoringScheduledWorkObserver(MonitoringExporter exporter) {
        this.exporter = exporter;
    }

    @Override
    public void observe(ScheduledWorkState state) {
        exporter.observe(state);
    }
}
