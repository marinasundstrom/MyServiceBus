package com.myservicebus.azure.servicebus;

import com.azure.messaging.servicebus.administration.ServiceBusAdministrationClient;
import com.azure.messaging.servicebus.administration.ServiceBusAdministrationClientBuilder;
import com.myservicebus.MessageBus;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

import java.net.URI;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class AzureServiceBusCloudTest {
    @Test
    void javaCreateModeProvisionsPublishesAndConsumes() throws Exception {
        String connectionString = System.getenv("AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING");
        Assumptions.assumeTrue(
                "1".equals(System.getenv("RUN_AZURE_SERVICEBUS_CLOUD_TESTS"))
                        && connectionString != null
                        && !connectionString.isBlank(),
                "Set RUN_AZURE_SERVICEBUS_CLOUD_TESTS=1 and "
                        + "AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING to run Azure cloud tests");

        String suffix = UUID.randomUUID().toString().replace("-", "").substring(0, 12);
        String queueName = "msb-cloud-java-" + suffix;
        String topicName = "msb-cloud-message-" + suffix;
        CompletableFuture<CloudMessage> received = new CompletableFuture<>();
        ServiceBusAdministrationClient administrationClient = new ServiceBusAdministrationClientBuilder()
                .connectionString(connectionString)
                .buildClient();
        MessageBus bus = MessageBus.factory.create(AzureServiceBusFactoryConfigurator.class, cfg -> {
            cfg.host(connectionString);
            cfg.message(CloudMessage.class, message -> message.setEntityName(topicName));
            cfg.receiveEndpoint(queueName, endpoint ->
                    endpoint.handler(CloudMessage.class, context -> {
                        received.complete(context.getMessage());
                        return CompletableFuture.completedFuture(null);
                    }));
        });

        try {
            bus.start();

            assertTrue(administrationClient.getQueueExists(queueName));
            assertTrue(administrationClient.getQueueExists(queueName + "_error"));
            assertTrue(administrationClient.getQueueExists(queueName + "_skipped"));
            assertTrue(administrationClient.getTopicExists(topicName));
            assertTrue(administrationClient.getTopicExists(queueName + "_fault"));
            assertEquals(
                    queueName,
                    entityName(administrationClient.getSubscription(topicName, queueName).getForwardTo()));

            CloudMessage message = new CloudMessage();
            message.setValue("java-live-azure");
            bus.publish(message).get(30, TimeUnit.SECONDS);

            assertEquals("java-live-azure", received.get(30, TimeUnit.SECONDS).getValue());
        } finally {
            bus.stop();
            deleteQueueIfExists(administrationClient, queueName);
            deleteQueueIfExists(administrationClient, queueName + "_error");
            deleteQueueIfExists(administrationClient, queueName + "_skipped");
            deleteTopicIfExists(administrationClient, topicName);
            deleteTopicIfExists(administrationClient, queueName + "_fault");
        }
    }

    private static void deleteQueueIfExists(
            ServiceBusAdministrationClient administrationClient,
            String queueName) {
        if (administrationClient.getQueueExists(queueName)) {
            administrationClient.deleteQueue(queueName);
        }
    }

    private static void deleteTopicIfExists(
            ServiceBusAdministrationClient administrationClient,
            String topicName) {
        if (administrationClient.getTopicExists(topicName)) {
            administrationClient.deleteTopic(topicName);
        }
    }

    private static String entityName(String address) {
        URI uri = URI.create(address);
        return uri.isAbsolute() ? uri.getPath().replaceFirst("^/", "") : address;
    }

    public static class CloudMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }
}
