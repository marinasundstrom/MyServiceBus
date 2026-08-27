package com.myservicebus.monitoring;

import javax.inject.Inject;

import com.myservicebus.BusHook;
import com.myservicebus.BusHookEvent;

final class MonitoringBusHook implements BusHook {
    private final MonitoringExporter exporter;

    @Inject
    MonitoringBusHook(MonitoringExporter exporter) {
        this.exporter = exporter;
    }

    @Override
    public void handle(BusHookEvent busEvent) {
        exporter.handle(busEvent);
    }
}
