package com.myservicebus.azure.servicebus;

import com.azure.messaging.servicebus.ServiceBusMessage;
import com.azure.messaging.servicebus.ServiceBusSenderClient;
import com.myservicebus.SendTransport;

import java.util.Map;

public final class AzureServiceBusSendTransport implements SendTransport {
    private final ServiceBusSenderClient sender;
    private final String entityName;

    AzureServiceBusSendTransport(ServiceBusSenderClient sender, String entityName) {
        this.sender = sender;
        this.entityName = entityName;
    }

    @Override
    public void send(byte[] data, Map<String, Object> headers, String contentType) {
        try {
            ServiceBusMessage message = AzureServiceBusMessageMapper.createMessage(data, headers, contentType);
            sender.sendMessage(message);
        } catch (Exception exception) {
            throw new AzureServiceBusTransportException("send", entityName, exception);
        }
    }

    void close() {
        sender.close();
    }
}
