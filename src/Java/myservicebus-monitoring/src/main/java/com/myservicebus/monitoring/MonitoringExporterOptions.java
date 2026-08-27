package com.myservicebus.monitoring;

import java.net.URI;
import java.time.Duration;

public final class MonitoringExporterOptions {
    private URI serviceAddress = URI.create("http://localhost:5310");
    private String applicationName = "MyServiceBus.Application";
    private String instanceId = System.getenv().getOrDefault("HOSTNAME", "java-" + ProcessHandle.current().pid());
    private String applicationVersion = "unknown";
    private String busId = "bus";
    private Duration exportInterval = Duration.ofSeconds(1);
    private Duration heartbeatInterval = Duration.ofSeconds(15);
    private int maxBatchSize = 256;
    private int maxQueueSize = 10_000;

    public URI getServiceAddress() {
        return serviceAddress;
    }

    public void setServiceAddress(URI serviceAddress) {
        this.serviceAddress = serviceAddress;
    }

    public String getApplicationName() {
        return applicationName;
    }

    public void setApplicationName(String applicationName) {
        this.applicationName = applicationName;
    }

    public String getInstanceId() {
        return instanceId;
    }

    public void setInstanceId(String instanceId) {
        this.instanceId = instanceId;
    }

    public String getApplicationVersion() {
        return applicationVersion;
    }

    public void setApplicationVersion(String applicationVersion) {
        this.applicationVersion = applicationVersion;
    }

    public String getBusId() {
        return busId;
    }

    public void setBusId(String busId) {
        this.busId = busId;
    }

    public Duration getExportInterval() {
        return exportInterval;
    }

    public void setExportInterval(Duration exportInterval) {
        this.exportInterval = exportInterval;
    }

    public Duration getHeartbeatInterval() {
        return heartbeatInterval;
    }

    public void setHeartbeatInterval(Duration heartbeatInterval) {
        this.heartbeatInterval = heartbeatInterval;
    }

    public int getMaxBatchSize() {
        return maxBatchSize;
    }

    public void setMaxBatchSize(int maxBatchSize) {
        this.maxBatchSize = maxBatchSize;
    }

    public int getMaxQueueSize() {
        return maxQueueSize;
    }

    public void setMaxQueueSize(int maxQueueSize) {
        this.maxQueueSize = maxQueueSize;
    }
}
