package com.myservicebus;

/**
 * Configures settings for a specific message type.
 *
 * @param <T> the message type
 */
public class MessageConfigurator<T> {
    private final Class<T> messageType;

    public MessageConfigurator(Class<T> messageType) {
        this.messageType = messageType;
    }

    /**
     * Overrides the entity (exchange) name used for the specified
     * message type. The value is stored in the {@link NamingConventions}
     * helper so that it can be retrieved wherever message names are
     * needed.
     *
     * @param entityName custom entity name
     */
    public void setEntityName(String entityName) {
        NamingConventions.setExchangeName(messageType, entityName);
    }

    Class<T> getMessageType() {
        return messageType;
    }
}

