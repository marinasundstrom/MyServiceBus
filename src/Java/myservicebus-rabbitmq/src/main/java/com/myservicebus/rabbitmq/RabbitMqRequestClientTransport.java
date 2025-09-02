package com.myservicebus.rabbitmq;

import java.net.InetAddress;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;

import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.Fault;
import com.myservicebus.HostInfo;
import com.myservicebus.NamingConventions;
import com.myservicebus.RequestClientTransport;
import com.myservicebus.tasks.CancellationToken;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.BuiltinExchangeType;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.rabbitmq.client.DeliverCallback;

/**
 * RabbitMQ transport implementation for request/response.
 */
public class RabbitMqRequestClientTransport implements RequestClientTransport {
    private final ConnectionProvider connectionProvider;
    private final ObjectMapper mapper;

    public RabbitMqRequestClientTransport(ConnectionProvider connectionProvider) {
        this.connectionProvider = connectionProvider;
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(Class<TRequest> requestType, TRequest request,
            Class<TResponse> responseType, CancellationToken cancellationToken) {
        CompletableFuture<TResponse> future = new CompletableFuture<>();
        try {
            Connection connection = connectionProvider.getOrCreateConnection();
            Channel channel = connection.createChannel();

            String responseExchange = "resp-" + UUID.randomUUID();
            String responseQueue = channel.queueDeclare().getQueue();
            channel.exchangeDeclare(responseExchange, BuiltinExchangeType.FANOUT, true);
            channel.queueBind(responseQueue, responseExchange, "");

            DeliverCallback callback = (tag, delivery) -> {
                try {
                    JavaType type = mapper.getTypeFactory().constructParametricType(Envelope.class, responseType);
                    Envelope<TResponse> envelope = mapper.readValue(delivery.getBody(), type);
                    future.complete(envelope.getMessage());
                } catch (Exception ex) {
                    try {
                        JavaType faultInner = mapper.getTypeFactory().constructParametricType(Fault.class, requestType);
                        JavaType faultType = mapper.getTypeFactory().constructParametricType(Envelope.class, faultInner);
                        Envelope<Fault<TRequest>> fault = mapper.readValue(delivery.getBody(), faultType);
                        String msg = fault.getMessage().getExceptions().isEmpty() ? "Request faulted"
                                : fault.getMessage().getExceptions().get(0).getMessage();
                        future.completeExceptionally(new RuntimeException(msg));
                    } catch (Exception inner) {
                        future.completeExceptionally(inner);
                    }
                } finally {
                    try {
                        channel.basicCancel(tag);
                        channel.queueDelete(responseQueue);
                        channel.close();
                    } catch (Exception ignore) {
                    }
                }
            };

            channel.basicConsume(responseQueue, true, callback, consumerTag -> {
            });

            String exchange = NamingConventions.getExchangeName(requestType);
            channel.exchangeDeclare(exchange, BuiltinExchangeType.FANOUT, true);

            Envelope<TRequest> envelope = new Envelope<>();
            envelope.setMessageId(UUID.randomUUID());
            envelope.setConversationId(UUID.randomUUID());
            envelope.setSentTime(OffsetDateTime.now());
            envelope.setDestinationAddress("rabbitmq://localhost/" + exchange);
            envelope.setResponseAddress("rabbitmq://localhost/" + responseExchange);
            envelope.setMessageType(List.of(NamingConventions.getMessageUrn(requestType)));
            envelope.setMessage(request);
            envelope.setHeaders(Map.of());
            envelope.setContentType("application/json");
            envelope.setHost(new HostInfo(
                    InetAddress.getLocalHost().getHostName(),
                    "java",
                    (int) ProcessHandle.current().pid(),
                    "my-app",
                    "1.0.0",
                    System.getProperty("java.version"),
                    "8.0.10.0",
                    System.getProperty("os.name") + " " + System.getProperty("os.version")));

            byte[] body = mapper.writeValueAsBytes(envelope);
            AMQP.BasicProperties props = new AMQP.BasicProperties.Builder()
                    .contentType("application/vnd.mybus.envelope+json")
                    .build();
            channel.basicPublish(exchange, "", props, body);
        } catch (Exception ex) {
            future.completeExceptionally(ex);
        }
        return future;
    }
}
