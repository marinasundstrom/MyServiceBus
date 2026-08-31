package com.myservicebus.monitoring;

import com.myservicebus.BusHook;
import com.myservicebus.ScheduledWorkObserver;
import com.myservicebus.di.ServiceCollection;

public final class MonitoringServices {
    private MonitoringServices() {
    }

    public static MonitoringExporter addMonitoring(ServiceCollection services, MonitoringExporterOptions options) {
        MonitoringExporter exporter = new MonitoringExporter(options);
        services.addSingleton(MonitoringExporter.class, ignored -> () -> exporter);
        services.addMultiBinding(BusHook.class, MonitoringBusHook.class);
        services.addMultiBinding(ScheduledWorkObserver.class, MonitoringScheduledWorkObserver.class);
        return exporter;
    }
}
