package com.myservicebus.azure.servicebus;

public final class AzureServiceBusTransportException extends RuntimeException {
    private final String operation;
    private final String entityName;

    public AzureServiceBusTransportException(String operation, String entityName, Throwable cause) {
        super("Azure Service Bus operation '" + operation + "' failed for entity '" + entityName + "'.", cause);
        this.operation = operation;
        this.entityName = entityName;
    }

    public String getOperation() {
        return operation;
    }

    public String getEntityName() {
        return entityName;
    }
}
