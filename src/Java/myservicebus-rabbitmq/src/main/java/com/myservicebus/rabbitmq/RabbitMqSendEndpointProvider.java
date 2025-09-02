package com.myservicebus.rabbitmq;

  import com.myservicebus.SendContext;
  import com.myservicebus.SendEndpoint;
  import com.myservicebus.SendEndpointProvider;
  import com.myservicebus.SendPipe;
  import com.myservicebus.SendTransport;
  import com.myservicebus.serialization.MessageSerializer;
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
          String exchange = uri.substring(uri.lastIndexOf('/') + 1);
          SendTransport transport = transportFactory.getSendTransport(exchange);
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
