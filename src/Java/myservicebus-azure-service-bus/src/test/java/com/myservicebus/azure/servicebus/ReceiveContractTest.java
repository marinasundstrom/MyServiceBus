package com.myservicebus.azure.servicebus;

import com.azure.core.util.BinaryData;
import com.azure.messaging.servicebus.*;
import com.myservicebus.logging.Slf4jLoggerFactory;
import org.junit.jupiter.api.Test;
import java.time.Duration;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;
import static org.mockito.ArgumentMatchers.*;

class ReceiveContractTest {
    @Test
    void matchesAllAdvertisedContractsBeforeSettlement() {
        for (boolean accepted : new boolean[] {true, false}) {
            var sender = mock(ServiceBusSenderClient.class);
            var context = mock(ServiceBusReceivedMessageContext.class);
            var message = mock(ServiceBusReceivedMessage.class);
            when(context.getMessage()).thenReturn(message);
            when(message.getBody()).thenReturn(BinaryData.fromString("""
                    {"messageId":"b4c5ac9a-29fe-4ae1-91ea-f8d13f5025e4","messageType":["urn:message:Contracts:Concrete","urn:message:Contracts:Base"],"message":{"value":1}}
                    """));
            when(message.getApplicationProperties()).thenReturn(Map.of());
            when(message.getTimeToLive()).thenReturn(Duration.ofHours(1));
            var handled = new AtomicInteger();
            var transport = new AzureServiceBusReceiveTransport(mock(ServiceBusProcessorClient.class), sender,
                    "input", body -> { handled.incrementAndGet(); return CompletableFuture.completedFuture(null); },
                    urn -> accepted && "urn:message:Contracts:Base".equals(urn), null, new Slf4jLoggerFactory());
            transport.process(context);
            assertEquals(accepted ? 1 : 0, handled.get());
            verify(sender, times(accepted ? 0 : 1)).sendMessage(any(ServiceBusMessage.class));
            verify(context).complete();
        }
    }
}
