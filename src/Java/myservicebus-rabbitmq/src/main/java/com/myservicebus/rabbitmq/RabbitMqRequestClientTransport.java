package com.myservicebus.rabbitmq;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;

import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.Fault;
import com.myservicebus.HostInfoProvider;
import com.myservicebus.NamingConventions;
import com.myservicebus.RequestFaultException;
import com.myservicebus.Response2;
import com.myservicebus.RequestClientTransport;
import com.myservicebus.SendContext;
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
    public <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(Class<TRequest> requestType,
            SendContext context,
            Class<TResponse> responseType) {
        CompletableFuture<TResponse> future = new CompletableFuture<>();
        try {
            Connection connection = connectionProvider.getOrCreateConnection();
            Channel channel = connection.createChannel();

            String responseExchange = "resp-" + UUID.randomUUID();
            String responseQueue = channel.queueDeclare().getQueue();
            channel.exchangeDeclare(responseExchange, BuiltinExchangeType.FANOUT, false, true, null);
            channel.queueBind(responseQueue, responseExchange, "");
            String address = "rabbitmq://localhost/exchange/" + responseExchange
                    + "?durable=false&autodelete=true";

            DeliverCallback callback = (tag, delivery) -> {
                try {
                    JavaType type = mapper.getTypeFactory().constructParametricType(Envelope.class, responseType);
                    Envelope<TResponse> envelope = mapper.readValue(delivery.getBody(), type);
                    future.complete(envelope.getMessage());
                } catch (Exception ex) {
                    try {
                        JavaType faultInner = mapper.getTypeFactory().constructParametricType(Fault.class, requestType);
                        JavaType faultType = mapper.getTypeFactory().constructParametricType(Envelope.class,
                                faultInner);
                        Envelope<Fault<TRequest>> fault = mapper.readValue(delivery.getBody(), faultType);
                        future.completeExceptionally(
                                new RequestFaultException(requestType.getSimpleName(), fault.getMessage()));
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
            envelope.setDestinationAddress("rabbitmq://localhost/exchange/" + exchange);
            envelope.setResponseAddress(address);
            envelope.setFaultAddress(address);
            envelope.setMessageType(List.of(NamingConventions.getMessageUrn(requestType)));
            @SuppressWarnings("unchecked")
            TRequest request = (TRequest) context.getMessage();
            envelope.setMessage(request);
            envelope.setHeaders(context.getHeaders());
            envelope.setContentType("application/json");
            envelope.setHost(HostInfoProvider.capture());

            byte[] body = mapper.writeValueAsBytes(envelope);
            AMQP.BasicProperties props = new AMQP.BasicProperties.Builder()
                    .contentType("application/vnd.masstransit+json")
                    .build();
            channel.basicPublish(exchange, "", props, body);
        } catch (Exception ex) {
            future.completeExceptionally(ex);
        }
        return future;
    }

    @Override
    public <TRequest, T1, T2> CompletableFuture<Response2<T1, T2>> sendRequest(Class<TRequest> requestType,
            SendContext context,
            Class<T1> responseType1, Class<T2> responseType2) {
        CompletableFuture<Response2<T1, T2>> future = new CompletableFuture<>();
        try {
            Connection connection = connectionProvider.getOrCreateConnection();
            Channel channel = connection.createChannel();

            String responseExchange = "resp-" + UUID.randomUUID();
            String responseQueue = channel.queueDeclare().getQueue();
            channel.exchangeDeclare(responseExchange, BuiltinExchangeType.FANOUT, false, true, null);
            channel.queueBind(responseQueue, responseExchange, "");
            String address = "rabbitmq://localhost/exchange/" + responseExchange
                    + "?durable=false&autodelete=true";

            DeliverCallback callback = (tag, delivery) -> {
                try {
                    try {
                        JavaType type1 = mapper.getTypeFactory().constructParametricType(Envelope.class, responseType1);
                        Envelope<T1> env1 = mapper.readValue(delivery.getBody(), type1);
                        future.complete(Response2.fromT1(env1.getMessage()));
                    } catch (Exception ex1) {
                        JavaType type2 = mapper.getTypeFactory().constructParametricType(Envelope.class, responseType2);
                        Envelope<T2> env2 = mapper.readValue(delivery.getBody(), type2);
                        future.complete(Response2.fromT2(env2.getMessage()));
                    }
                } catch (Exception ex) {
                    try {
                        JavaType faultInner = mapper.getTypeFactory().constructParametricType(Fault.class, requestType);
                        JavaType faultType = mapper.getTypeFactory().constructParametricType(Envelope.class,
                                faultInner);
                        Envelope<Fault<TRequest>> fault = mapper.readValue(delivery.getBody(), faultType);
                        future.completeExceptionally(
                                new RequestFaultException(requestType.getSimpleName(), fault.getMessage()));
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
            envelope.setDestinationAddress("rabbitmq://localhost/exchange/" + exchange);
            envelope.setResponseAddress(address);
            envelope.setFaultAddress(address);
            envelope.setMessageType(List.of(NamingConventions.getMessageUrn(requestType)));
            @SuppressWarnings("unchecked")
            TRequest request = (TRequest) context.getMessage();
            envelope.setMessage(request);
            envelope.setHeaders(context.getHeaders());
            envelope.setContentType("application/json");
            envelope.setHost(HostInfoProvider.capture());

            byte[] body = mapper.writeValueAsBytes(envelope);
            AMQP.BasicProperties props = new AMQP.BasicProperties.Builder()
                    .contentType("application/vnd.masstransit+json")
                    .build();
            channel.basicPublish(exchange, "", props, body);
        } catch (Exception ex) {
            future.completeExceptionally(ex);
        }
        return future;
    }
}
