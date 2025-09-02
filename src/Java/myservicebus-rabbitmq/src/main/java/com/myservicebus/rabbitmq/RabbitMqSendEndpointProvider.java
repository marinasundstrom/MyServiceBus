package com.myservicebus.rabbitmq;

  import com.myservicebus.SendContext;
  import com.myservicebus.SendEndpoint;
  import com.myservicebus.SendEndpointProvider;
  import com.myservicebus.SendPipe;
  import com.myservicebus.SendTransport;
import com.myservicebus.serialization.MessageSerializer;
import java.net.URI;
import java.util.concurrent.CompletableFuture;

/**
 * Provides send endpoints backed by RabbitMQ transports.
 */
  public class RabbitMqSendEndpointProvider implements SendEndpointProvider {
      private final RabbitMqTransportFactory transportFactory;
      private final SendPipe sendPipe;
      private final MessageSerializer serializer;

      public RabbitMqSendEndpointProvider(RabbitMqTransportFactory transportFactory, SendPipe sendPipe, MessageSerializer serializer) {
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
            transport = transportFactory.getSendTransport(exchange);
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
              public <T> CompletableFuture<Void> send(T message, com.myservicebus.tasks.CancellationToken cancellationToken) {
                  return send(new SendContext(message, cancellationToken));
              }
          };
      }
  }
