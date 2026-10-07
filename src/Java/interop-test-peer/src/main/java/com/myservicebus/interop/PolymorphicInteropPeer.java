package com.myservicebus.interop;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.*;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

/** Uses normal bus registration and publishing; local names intentionally differ from C#. */
public final class PolymorphicInteropPeer {
    @EntityName("acme-order")
    @MessageUrnName(value = "urn:message:Acme:JavaLocalOnly", useDefaultPrefix = false)
    public interface JavaOrder { int getQuantity(); }

    @EntityName("acme-base-order")
    @MessageUrnName(value = "urn:message:Acme:BaseOrder", useDefaultPrefix = false)
    public static class JavaBase { public int quantity; }

    @EntityName("acme-submitted-order")
    @MessageUrnName(value = "urn:message:Acme:SubmittedOrder", useDefaultPrefix = false)
    public static class JavaSubmitted extends JavaBase implements JavaOrder {
        public int getQuantity() { return quantity; }
    }

    public static final class JavaConsumer implements Consumer<JavaOrder> {
        static final CompletableFuture<Integer> received = new CompletableFuture<>();
        public CompletableFuture<Void> consume(ConsumeContext<JavaOrder> context) {
            if (context.getMessageId() == null) return CompletableFuture.failedFuture(new AssertionError("missing messageId"));
            received.complete(context.getMessage().getQuantity());
            return CompletableFuture.completedFuture(null);
        }
    }

    public static void run(String[] args) throws Exception {
        var services = ServiceCollection.create();
        var rabbit = new RabbitMqFactoryConfigurator();
        rabbit.host(System.getenv("RABBITMQ_HOST"), Integer.parseInt(System.getenv("RABBITMQ_PORT")), host -> {
            host.username(System.getenv("RABBITMQ_USERNAME"));
            host.password(System.getenv("RABBITMQ_PASSWORD"));
        });
        var bus = MessageBusImpl.configure(services, cfg -> {
            cfg.setMessageUrn(JavaOrder.class, "urn:message:Acme:Order");
            if (args[0].equals("polymorphic-consume")) cfg.addConsumer(JavaConsumer.class);
            RabbitMqTransport.configure(cfg, rabbit);
        });
        try {
            bus.start();
            if (args[0].equals("polymorphic-consume")) {
                System.out.println("READY");
                int value = JavaConsumer.received.get(30, TimeUnit.SECONDS);
                if (value != Integer.parseInt(args[3])) throw new AssertionError("Unexpected quantity: " + value);
                System.out.println("RECEIVED:" + value);
            } else {
                var message = new JavaSubmitted();
                message.quantity = Integer.parseInt(args[3]);
                bus.publish(message).join();
                System.out.println("PUBLISHED");
            }
        } finally { bus.stop(); }
        System.exit(0); // Match the interop harness: the shared RabbitMQ connection has process lifetime.
    }
}
