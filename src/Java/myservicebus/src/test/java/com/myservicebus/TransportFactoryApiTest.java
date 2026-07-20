package com.myservicebus;

import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import org.junit.jupiter.api.Test;

import java.net.URI;
import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertNotNull;

class TransportFactoryApiTest {
    @Test
    void newTransportCanImplementOnlyProfileNeutralReceiveTopology() throws Exception {
        TransportFactory factory = new IntentOnlyTransportFactory();
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(Object.class);
        binding.setEntityName("Contracts:OrderSubmitted");
        ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                "orders", true, false, 0, List.of(binding), Map.of());

        ReceiveTransport transport = factory.createReceiveTransport(
                topology, message -> java.util.concurrent.CompletableFuture.completedFuture(null), urn -> true);

        assertNotNull(transport);
    }

    private static final class IntentOnlyTransportFactory implements TransportFactory {
        @Override
        public SendTransport getSendTransport(URI address) {
            throw new UnsupportedOperationException();
        }

        @Override
        public ReceiveTransport createReceiveTransport(
                ReceiveEndpointTransportTopology topology,
                java.util.function.Function<TransportMessage, java.util.concurrent.CompletableFuture<Void>> handler,
                java.util.function.Function<String, Boolean> isMessageTypeRegistered) {
            return new ReceiveTransport() {
                @Override
                public void start() {
                }

                @Override
                public void stop() {
                }
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "exchange:" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "queue:" + queue;
        }
    }
}
