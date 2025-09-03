package com.myservicebus.topology;

public class MessageTopology {
    private Class<?> messageType;
    private String entityName;

    public Class<?> getMessageType() {
        return messageType;
    }

    public void setMessageType(Class<?> messageType) {
        this.messageType = messageType;
    }

    public String getEntityName() {
        return entityName;
    }

    public void setEntityName(String entityName) {
        this.entityName = entityName;
    }
}
