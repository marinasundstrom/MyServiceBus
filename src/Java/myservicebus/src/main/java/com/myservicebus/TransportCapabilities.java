package com.myservicebus;

public final class TransportCapabilities {
    public static final String DIRECTED_SEND = "directedSend";
    public static final String PUBLISH_SUBSCRIBE = "publishSubscribe";
    public static final String DURABILITY = "durability";
    public static final String COMPETING_CONSUMERS = "competingConsumers";
    public static final String ACKNOWLEDGEMENT = "acknowledgement";
    public static final String REQUEST_RESPONSE = "requestResponse";
    public static final String SCHEDULING = "scheduling";
    public static final String REDELIVERY = "redelivery";
    public static final String ERROR_DESTINATIONS = "errorDestinations";
    public static final String ORDERING = "ordering";
    public static final String REPLAY = "replay";
    public static final String TEMPORARY_ENDPOINTS = "temporaryEndpoints";
    public static final String TOPOLOGY_PROVISIONING = "topologyProvisioning";

    private TransportCapabilities() {
    }
}
