package com.myservicebus.azure.servicebus;

import com.azure.messaging.servicebus.ServiceBusProcessorClient;
import com.azure.messaging.servicebus.ServiceBusSenderClient;
import com.myservicebus.BusStopTimeoutException;
import com.myservicebus.logging.Slf4jLoggerFactory;
import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.util.concurrent.CountDownLatch;

import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTimeoutPreemptively;
import static org.mockito.Mockito.doAnswer;
import static org.mockito.Mockito.mock;

class AzureServiceBusReceiveTransportTest {
    @Test
    void timedStopRemainsBoundedWhenProcessorStopDoesNotReturn() {
        ServiceBusProcessorClient processor = mock(ServiceBusProcessorClient.class);
        ServiceBusSenderClient skippedSender = mock(ServiceBusSenderClient.class);
        CountDownLatch releaseProcessor = new CountDownLatch(1);
        doAnswer(invocation -> {
            releaseProcessor.await();
            return null;
        }).when(processor).stop();
        AzureServiceBusReceiveTransport transport = new AzureServiceBusReceiveTransport(
                processor,
                skippedSender,
                "input",
                message -> java.util.concurrent.CompletableFuture.completedFuture(null),
                ignored -> true,
                "fault",
                new Slf4jLoggerFactory());

        try {
            assertTimeoutPreemptively(
                    Duration.ofSeconds(1),
                    () -> assertThrows(
                            BusStopTimeoutException.class,
                            () -> transport.stop(Duration.ofMillis(50))));
        } finally {
            releaseProcessor.countDown();
        }
    }
}
