package com.myservicebus.amazon.sqs;

import com.myservicebus.logging.Slf4jLoggerFactory;
import org.junit.jupiter.api.Test;
import software.amazon.awssdk.services.sqs.SqsClient;
import software.amazon.awssdk.services.sqs.model.Message;
import software.amazon.awssdk.services.sqs.model.SendMessageRequest;
import software.amazon.awssdk.services.sqs.model.DeleteMessageRequest;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Consumer;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;
import static org.mockito.ArgumentMatchers.*;

class ReceiveContractTest {
    @Test
    void matchesAllAdvertisedContractsBeforeSettlement() throws Exception {
        for (boolean accepted : new boolean[] {true, false}) {
            SqsClient sqs = mock(SqsClient.class);
            var handled = new AtomicInteger();
            var transport = new AmazonSqsReceiveTransport(sqs, "input", "skipped", "input", false,
                    1, 30, 1, 1, message -> { handled.incrementAndGet(); return CompletableFuture.completedFuture(null); },
                    urn -> accepted && "urn:message:Contracts:Base".equals(urn), null, new Slf4jLoggerFactory());
            var message = Message.builder().receiptHandle("receipt").body("""
                    {"messageId":"b4c5ac9a-29fe-4ae1-91ea-f8d13f5025e4","messageType":["urn:message:Contracts:Concrete","urn:message:Contracts:Base"],"message":{"value":1}}
                    """).build();
            try {
                var process = AmazonSqsReceiveTransport.class.getDeclaredMethod("process", Message.class);
                process.setAccessible(true);
                process.invoke(transport, message);
                assertEquals(accepted ? 1 : 0, handled.get());
                verify(sqs, times(accepted ? 0 : 1)).sendMessage(any(SendMessageRequest.class));
                verify(sqs).deleteMessage(org.mockito.ArgumentMatchers.<Consumer<DeleteMessageRequest.Builder>>any());
            } finally { transport.stop(); }
        }
    }
}
