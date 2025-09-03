package com.myservicebus.rabbitmq;

import com.myservicebus.SendContext;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendPipe;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.serialization.MessageSerializer;
import java.net.URI;
import java.util.concurrent.CompletableFuture;

public class RabbitMqSendEndpointProvider implements TransportSendEndpointProvider {
    private final RabbitMqTransportFactory transportFactory;
    private final SendPipe sendPipe;
    private final MessageSerializer serializer;

    public RabbitMqSendEndpointProvider(RabbitMqTransportFactory transportFactory, SendPipe sendPipe,
            MessageSerializer serializer) {
        this.transportFactory = transportFactory;
        this.sendPipe = sendPipe;
        this.serializer = serializer;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        URI target = URI.create(uri);
        String path = target.getPath();
        SendTransport transport;
        if (path.startsWith("/exchange/")) {
            String exchange = path.substring("/exchange/".length());
            boolean durable = true;
            boolean autoDelete = false;
            String query = target.getQuery();
            if (query != null) {
                String[] parts = query.split("&");
                for (String part : parts) {
                    String[] kv = part.split("=", 2);
                    if (kv.length == 2) {
                        if (kv[0].equalsIgnoreCase("durable")) {
                            durable = Boolean.parseBoolean(kv[1]);
                        } else if (kv[0].equalsIgnoreCase("autodelete")) {
                            autoDelete = Boolean.parseBoolean(kv[1]);
                        }
                    }
                }
            }
            transport = transportFactory.getSendTransport(exchange, durable, autoDelete);
        } else {
            String queue = path.startsWith("/") ? path.substring(1) : path;
            transport = transportFactory.getQueueTransport(queue);
        }
        RabbitMqSendEndpoint endpoint = new RabbitMqSendEndpoint(transport, serializer);
        return new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext ctx) {
                return sendPipe.send(ctx).thenCompose(v -> endpoint.send(ctx));
            }

            @Override
            public <T> CompletableFuture<Void> send(T message,
                    com.myservicebus.tasks.CancellationToken cancellationToken) {
                return send(new SendContext(message, cancellationToken));
            }
        };
    }
}
