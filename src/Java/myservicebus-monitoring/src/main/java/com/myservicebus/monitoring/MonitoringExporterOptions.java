package com.myservicebus.monitoring;

import java.net.URI;
import java.time.Duration;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.function.BiFunction;
import java.util.function.Predicate;

public final class MonitoringExporterOptions {
    private URI serviceAddress = URI.create("http://localhost:5310");
    private String applicationName = "MyServiceBus.Application";
    private String instanceId = System.getenv().getOrDefault("HOSTNAME", "java-" + ProcessHandle.current().pid());
    private String applicationVersion = "unknown";
    private String busId = "bus";
    private final Map<String, String> labels = new LinkedHashMap<>();
    private Duration exportInterval = Duration.ofSeconds(1);
    private Duration heartbeatInterval = Duration.ofSeconds(15);
    private int maxBatchSize = 256;
    private int maxQueueSize = 10_000;
    private int maxScheduledWorkItems = 1_000;
    private int maxJobItems = 1_000;
    private int maxJobAttempts = 10;
    private Duration scheduledWorkHistory = Duration.ofHours(24);
    private MonitoringCaptureProfile captureProfile = MonitoringCaptureProfile.AUTO;
    private Boolean captureMessageIdentity;
    private Boolean captureCorrelationIdentity;
    private Boolean captureRequestResponseMetadata;
    private Boolean captureAddresses;
    private Boolean captureExceptionMessages;
    private boolean captureMessageBodies;
    private int maxMessageBodyBytes = 16 * 1024;
    private Predicate<String> messageBodyTypeFilter;
    private BiFunction<String, String, String> messageBodyRedactor;

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

    public Map<String, String> getLabels() {
        return labels;
    }

    public void setLabels(Map<String, String> labels) {
        this.labels.clear();
        if (labels != null) {
            this.labels.putAll(labels);
        }
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

    public int getMaxScheduledWorkItems() {
        return maxScheduledWorkItems;
    }

    public void setMaxScheduledWorkItems(int maxScheduledWorkItems) {
        this.maxScheduledWorkItems = maxScheduledWorkItems;
    }

    public int getMaxJobItems() {
        return maxJobItems;
    }

    public void setMaxJobItems(int maxJobItems) {
        this.maxJobItems = maxJobItems;
    }

    public int getMaxJobAttempts() {
        return maxJobAttempts;
    }

    public void setMaxJobAttempts(int maxJobAttempts) {
        this.maxJobAttempts = maxJobAttempts;
    }

    public Duration getScheduledWorkHistory() {
        return scheduledWorkHistory;
    }

    public void setScheduledWorkHistory(Duration scheduledWorkHistory) {
        this.scheduledWorkHistory = scheduledWorkHistory;
    }

    public MonitoringCaptureProfile getCaptureProfile() {
        return captureProfile;
    }

    public void setCaptureProfile(MonitoringCaptureProfile captureProfile) {
        this.captureProfile = captureProfile;
    }

    public Boolean getCaptureMessageIdentity() {
        return captureMessageIdentity;
    }

    public void setCaptureMessageIdentity(boolean captureMessageIdentity) {
        this.captureMessageIdentity = captureMessageIdentity;
    }

    public Boolean getCaptureCorrelationIdentity() {
        return captureCorrelationIdentity;
    }

    public void setCaptureCorrelationIdentity(boolean captureCorrelationIdentity) {
        this.captureCorrelationIdentity = captureCorrelationIdentity;
    }

    public Boolean getCaptureRequestResponseMetadata() {
        return captureRequestResponseMetadata;
    }

    public void setCaptureRequestResponseMetadata(boolean captureRequestResponseMetadata) {
        this.captureRequestResponseMetadata = captureRequestResponseMetadata;
    }

    public Boolean getCaptureAddresses() {
        return captureAddresses;
    }

    public void setCaptureAddresses(boolean captureAddresses) {
        this.captureAddresses = captureAddresses;
    }

    public Boolean getCaptureExceptionMessages() {
        return captureExceptionMessages;
    }

    public void setCaptureExceptionMessages(boolean captureExceptionMessages) {
        this.captureExceptionMessages = captureExceptionMessages;
    }

    public boolean isCaptureMessageBodies() {
        return captureMessageBodies;
    }

    public void setCaptureMessageBodies(boolean captureMessageBodies) {
        this.captureMessageBodies = captureMessageBodies;
    }

    public int getMaxMessageBodyBytes() {
        return maxMessageBodyBytes;
    }

    public void setMaxMessageBodyBytes(int maxMessageBodyBytes) {
        this.maxMessageBodyBytes = maxMessageBodyBytes;
    }

    public Predicate<String> getMessageBodyTypeFilter() {
        return messageBodyTypeFilter;
    }

    public void setMessageBodyTypeFilter(Predicate<String> messageBodyTypeFilter) {
        this.messageBodyTypeFilter = messageBodyTypeFilter;
    }

    public BiFunction<String, String, String> getMessageBodyRedactor() {
        return messageBodyRedactor;
    }

    public void setMessageBodyRedactor(BiFunction<String, String, String> messageBodyRedactor) {
        this.messageBodyRedactor = messageBodyRedactor;
    }

    boolean captureSensitiveData(Boolean override) {
        if (override != null) {
            return override;
        }
        return switch (captureProfile) {
            case DEVELOPMENT -> true;
            case PRODUCTION -> false;
            case AUTO -> isDevelopmentEnvironment();
        };
    }

    private static boolean isDevelopmentEnvironment() {
        String environment = System.getenv("MYSERVICEBUS_ENVIRONMENT");
        return environment != null && environment.equalsIgnoreCase("Development");
    }
}
