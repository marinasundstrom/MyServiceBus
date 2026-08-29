package com.myservicebus.amazon.sqs;

public final class AmazonSqsTransportException extends RuntimeException {
    private final String operation;
    private final String entityName;

    public AmazonSqsTransportException(String operation, String entityName, Throwable cause) {
        super("Amazon SQS/SNS could not " + operation + " for entity '" + entityName + "'.", cause);
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
