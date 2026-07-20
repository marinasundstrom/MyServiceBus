package com.myservicebus.interop;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.ExceptionInfo;
import com.myservicebus.Fault;
import com.myservicebus.MessageUrn;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendContext;
import com.myservicebus.SendTransport;
import com.myservicebus.RequestFaultException;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.ConnectionProvider;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import com.myservicebus.rabbitmq.RabbitMqRequestClientTransport;
import com.myservicebus.rabbitmq.RabbitMqTransport;
import com.myservicebus.rabbitmq.RabbitMqTransportFactory;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.rabbitmq.client.ConnectionFactory;

import java.util.List;
import java.net.URI;
import java.time.OffsetDateTime;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;

public final class InteropTestPeer {
    private InteropTestPeer() {
    }

    public static void main(String[] args) throws Exception {
        if (args.length != 5) {
            throw new IllegalArgumentException(
                    "Expected: <consume|consume-unrecognized|consume-fault|produce|send|request|request-fault|respond|fault> <exchange> <queue> <value> <durable-exchange>");
        }

        String host = requiredEnvironment("RABBITMQ_HOST");
        int port = Integer.parseInt(requiredEnvironment("RABBITMQ_PORT"));
        String username = requiredEnvironment("RABBITMQ_USERNAME");
        String password = requiredEnvironment("RABBITMQ_PASSWORD");
        ConnectionProvider connectionProvider = createConnectionProvider(host, port, username, password);
        RabbitMqTransportFactory transportFactory = createTransportFactory(
                connectionProvider, host, port, username, password);

        if ("consume".equals(args[0])) {
            consume(transportFactory, args[1], args[2], args[3]);
        } else if ("consume-unrecognized".equals(args[0])) {
            consumeUnrecognized(transportFactory, args[1], args[2]);
        } else if ("consume-fault".equals(args[0])) {
            consumeFault(host, port, username, password, args[1], args[2]);
        } else if ("produce".equals(args[0])) {
            produce(transportFactory, args[1], args[3], Boolean.parseBoolean(args[4]));
        } else if ("send".equals(args[0])) {
            send(transportFactory, args[2], args[3]);
        } else if ("request".equals(args[0])) {
            request(connectionProvider, args[3]);
        } else if ("request-fault".equals(args[0])) {
            requestFault(connectionProvider, args[3]);
        } else if ("respond".equals(args[0])) {
            reply(transportFactory, args[1], args[2], args[3], false);
        } else if ("fault".equals(args[0])) {
            reply(transportFactory, args[1], args[2], args[3], true);
        } else {
            throw new IllegalArgumentException("Unknown mode: " + args[0]);
        }
    }

    private static void consumeUnrecognized(
            RabbitMqTransportFactory transportFactory, String exchangeName, String queueName) throws Exception {
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(InteropRequest.class);
        binding.setEntityName(exchangeName);
        ReceiveTransport receiveTransport = transportFactory.createReceiveTransport(
                queueName,
                List.of(binding),
                transportMessage -> CompletableFuture.failedFuture(
                        new IllegalStateException("An unrecognized message must not reach the handler.")),
                ignored -> false,
                1);

        receiveTransport.start();
        System.out.println("READY");
        System.out.flush();
        try {
            Thread.sleep(TimeUnit.SECONDS.toMillis(30));
        } finally {
            receiveTransport.stop();
        }
    }

    private static void consumeFault(
            String host, int port, String username, String password, String exchangeName, String queueName)
            throws Exception {
        RabbitMqFactoryConfigurator rabbit = new RabbitMqFactoryConfigurator();
        rabbit.host(host, port, credentials -> {
            credentials.username(username);
            credentials.password(password);
        });
        ServiceCollection services = ServiceCollection.create();
        MessageBus bus = MessageBusImpl.configure(services, registration -> RabbitMqTransport.configure(registration, rabbit));
        AtomicInteger attempts = new AtomicInteger();
        CompletableFuture<Void> exhausted = new CompletableFuture<>();
        ((MessageBusImpl) bus).addHandler(
                queueName,
                CrossLanguageMessage.class,
                exchangeName,
                context -> {
                    if (attempts.incrementAndGet() == 3) {
                        exhausted.complete(null);
                    }
                    return CompletableFuture.failedFuture(new IllegalStateException("retry-exhausted"));
                },
                2,
                null,
                1,
                null,
                null);

        bus.start();
        System.out.println("READY");
        System.out.flush();
        try {
            exhausted.get(20, TimeUnit.SECONDS);
            System.out.println("EXHAUSTED");
            System.out.flush();
            Thread.sleep(TimeUnit.SECONDS.toMillis(3));
        } finally {
            bus.stop();
        }
        System.exit(0);
    }

    private static ConnectionProvider createConnectionProvider(
            String host, int port, String username, String password) {
        ConnectionFactory connectionFactory = new ConnectionFactory();
        connectionFactory.setHost(host);
        connectionFactory.setPort(port);
        connectionFactory.setUsername(username);
        connectionFactory.setPassword(password);

        return new ConnectionProvider(connectionFactory);
    }

    private static RabbitMqTransportFactory createTransportFactory(
            ConnectionProvider connectionProvider, String host, int port, String username, String password) {

        RabbitMqFactoryConfigurator configurator = new RabbitMqFactoryConfigurator();
        configurator.host(host, port, credentials -> {
            credentials.username(username);
            credentials.password(password);
        });
        return new RabbitMqTransportFactory(
                connectionProvider,
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));
    }

    private static void consume(RabbitMqTransportFactory transportFactory, String exchangeName, String queueName,
            String expectedValue) throws Exception {
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(CrossLanguageMessage.class);
        binding.setEntityName(exchangeName);

        CompletableFuture<String> received = new CompletableFuture<>();
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        ReceiveTransport receiveTransport = transportFactory.createReceiveTransport(
                queueName,
                List.of(binding),
                transportMessage -> {
                    try {
                        Envelope<CrossLanguageMessage> envelope = mapper.readValue(
                                transportMessage.getBody(),
                                new TypeReference<>() { });
                        received.complete(envelope.getMessage().getValue());
                        return CompletableFuture.completedFuture(null);
                    } catch (Exception exception) {
                        return CompletableFuture.failedFuture(exception);
                    }
                },
                MessageUrn.forClass(CrossLanguageMessage.class)::equals,
                1);

        receiveTransport.start();
        System.out.println("READY");
        System.out.flush();
        try {
            String actualValue = received.get(20, TimeUnit.SECONDS);
            if (!expectedValue.equals(actualValue)) {
                throw new IllegalStateException("Expected '" + expectedValue + "' but received '" + actualValue + "'");
            }
            System.out.println("RECEIVED");
            System.out.flush();
        } finally {
            receiveTransport.stop();
        }
        System.exit(0);
    }

    private static void produce(
            RabbitMqTransportFactory transportFactory, String exchangeName, String value, boolean durableExchange)
            throws Exception {
        CrossLanguageMessage message = new CrossLanguageMessage();
        message.setValue(value);
        SendContext context = new SendContext(message, CancellationToken.none);
        byte[] body = context.serialize(new EnvelopeMessageSerializer());
        SendTransport sendTransport = transportFactory.getSendTransport(exchangeName, durableExchange, !durableExchange);
        sendTransport.send(body);
        System.out.println("SENT");
        System.out.flush();
        System.exit(0);
    }

    private static void send(RabbitMqTransportFactory transportFactory, String queueName, String value)
            throws Exception {
        CrossLanguageMessage message = new CrossLanguageMessage();
        message.setValue(value);
        SendContext context = new SendContext(message, CancellationToken.none);
        byte[] body = context.serialize(new EnvelopeMessageSerializer());
        transportFactory.getQueueTransport(queueName).send(body);
        System.out.println("SENT");
        System.out.flush();
        System.exit(0);
    }

    private static void request(ConnectionProvider connectionProvider, String value) throws Exception {
        InteropRequest request = new InteropRequest();
        request.setValue(value);
        SendContext context = new SendContext(request, CancellationToken.none);
        RabbitMqRequestClientTransport transport = new RabbitMqRequestClientTransport(connectionProvider);
        InteropResponse response = transport.sendRequest(InteropRequest.class, context, InteropResponse.class)
                .get(20, TimeUnit.SECONDS);
        if (!"response-from-masstransit".equals(response.getValue())) {
            throw new IllegalStateException("Unexpected response: " + response.getValue());
        }
        System.out.println("RECEIVED");
        System.out.flush();
        System.exit(0);
    }

    private static void requestFault(ConnectionProvider connectionProvider, String value) throws Exception {
        InteropRequest request = new InteropRequest();
        request.setValue(value);
        SendContext context = new SendContext(request, CancellationToken.none);
        RabbitMqRequestClientTransport transport = new RabbitMqRequestClientTransport(connectionProvider);
        try {
            transport.sendRequest(InteropRequest.class, context, InteropResponse.class)
                    .get(20, TimeUnit.SECONDS);
            throw new IllegalStateException("Expected the request to fault.");
        } catch (ExecutionException exception) {
            if (!(exception.getCause() instanceof RequestFaultException requestFault)
                    || !requestFault.getMessage().contains("mass-transit-fault")) {
                exception.getCause().printStackTrace(System.err);
                System.err.flush();
                System.exit(2);
            }
        }
        System.out.println("FAULT");
        System.out.flush();
        System.exit(0);
    }

    private static void reply(RabbitMqTransportFactory transportFactory, String exchangeName, String queueName,
            String expectedValue, boolean sendFault) throws Exception {
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(InteropRequest.class);
        binding.setEntityName(exchangeName);

        CompletableFuture<Void> responded = new CompletableFuture<>();
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        ReceiveTransport receiveTransport = transportFactory.createReceiveTransport(
                queueName,
                List.of(binding),
                transportMessage -> {
                    try {
                        Envelope<InteropRequest> requestEnvelope = mapper.readValue(
                                transportMessage.getBody(), new TypeReference<>() { });
                        if (!expectedValue.equals(requestEnvelope.getMessage().getValue())) {
                            throw new IllegalStateException("Unexpected request: "
                                    + requestEnvelope.getMessage().getValue());
                        }

                        byte[] body = sendFault
                                ? createFaultEnvelope(mapper, requestEnvelope)
                                : createResponseEnvelope(requestEnvelope);
                        transportFactory.getSendTransport(URI.create(requestEnvelope.getResponseAddress())).send(body);
                        responded.complete(null);
                        return CompletableFuture.completedFuture(null);
                    } catch (Exception exception) {
                        responded.completeExceptionally(exception);
                        return CompletableFuture.failedFuture(exception);
                    }
                },
                MessageUrn.forClass(InteropRequest.class)::equals,
                1);

        receiveTransport.start();
        System.out.println("READY");
        System.out.flush();
        try {
            responded.get(20, TimeUnit.SECONDS);
            System.out.println(sendFault ? "FAULTED" : "RESPONDED");
            System.out.flush();
        } finally {
            receiveTransport.stop();
        }
        System.exit(0);
    }

    private static byte[] createResponseEnvelope(Envelope<InteropRequest> requestEnvelope) throws Exception {
        InteropResponse response = new InteropResponse();
        response.setValue("response-from-java");
        SendContext responseContext = new SendContext(response, CancellationToken.none);
        responseContext.setRequestId(requestEnvelope.getRequestId());
        responseContext.setDestinationAddress(URI.create(requestEnvelope.getResponseAddress()));
        return responseContext.serialize(new EnvelopeMessageSerializer());
    }

    private static byte[] createFaultEnvelope(
            ObjectMapper mapper, Envelope<InteropRequest> requestEnvelope) throws Exception {
        Fault<InteropRequest> fault = new Fault<>();
        fault.setMessage(requestEnvelope.getMessage());
        fault.setFaultId(UUID.randomUUID());
        fault.setMessageId(requestEnvelope.getMessageId());
        fault.setSentTime(OffsetDateTime.now());
        fault.setExceptions(List.of(ExceptionInfo.fromException(
                new IllegalStateException("java-fault"))));

        Envelope<Fault<InteropRequest>> envelope = new Envelope<>();
        envelope.setMessageId(UUID.randomUUID());
        envelope.setRequestId(requestEnvelope.getRequestId());
        envelope.setDestinationAddress(requestEnvelope.getResponseAddress());
        envelope.setSentTime(OffsetDateTime.now());
        envelope.setMessageType(List.of(MessageUrn.forFault(InteropRequest.class)));
        envelope.setMessage(fault);
        envelope.setContentType("application/json");
        return mapper.writeValueAsBytes(envelope);
    }

    private static String requiredEnvironment(String name) {
        String value = System.getenv(name);
        if (value == null || value.isBlank()) {
            throw new IllegalStateException(name + " is required");
        }
        return value;
    }

    public static class CrossLanguageMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }

    public static class InteropRequest {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }

    public static class InteropResponse {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }
}
