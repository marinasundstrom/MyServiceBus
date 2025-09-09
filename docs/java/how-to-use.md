# How to use

The Service Bus API is still in the works.

## Configure Service Bus

We use `ServiceCollection` to register services that will supplied to the `Consumers`.

```java
package com.myservicebus.testapp;

import java.util.UUID;

import com.myservicebus.MyService;
import com.myservicebus.MyServiceImpl;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;

public class Main {
    public static void main(String[] args) throws Exception {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(MyService.class, MyServiceImpl.class);

        services.from(MessageBusServices.class)
                .addServiceBus(RabbitMqFactoryConfigurator.class, cfg -> {
                    cfg.addConsumer(SubmitOrderConsumer.class);
                    cfg.usingRabbitMq((context, rbCfg) -> {
                        rbCfg.host("rabbitmq://localhost");
                        rbCfg.receiveEndpoint("submit-order-queue", e -> {
                            e.configureConsumer(context, SubmitOrderConsumer.class);
                        });
                    });
                });

        ServiceProvider provider = services.buildServiceProvider();
        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider sp = scope.getServiceProvider();
            MessageBus bus = sp.getService(MessageBus.class);

            bus.start();

            System.out.println("Up and running");

            SubmitOrder message = new SubmitOrder(UUID.randomUUID());

            bus.publish(message);

            System.out.println("Waiting");

            System.in.read();
        }
    }
}
```

## Request/Response

```java
RequestClient<SubmitOrder> client = serviceProvider.getService(RequestClient.class);
OrderSubmitted response = client
        .getResponse(new SubmitOrder(UUID.randomUUID(), "demo"), OrderSubmitted.class, CancellationToken.none)
        .get();
```

## Defining messages

We are using `lombok` to generate property getters and setters, as well as default constructors, for messages.

If you are using Gradle:

```gradle
dependencies {
    compileOnly 'org.projectlombok:lombok:1.18.30'
    annotationProcessor 'org.projectlombok:lombok:1.18.30'
}
```

### Request message

```java
package com.myservicebus.testapp;

import java.util.UUID;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class SubmitOrder {
    private UUID orderId;
}
```

### Event message

```java
package com.myservicebus.testapp;

import java.util.UUID;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class OrderSubmitted {
    private UUID orderId;
}
```

## The Consumer

```java
package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.MyService;
import com.myservicebus.tasks.CancellationToken;

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    private MyService service;

    @Inject
    public SubmitOrderConsumer(MyService service) {
        this.service = service;
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) throws Exception {
        var orderId = context
                .getMessage()
                .getOrderId();

        service.doWork();

        System.out.println("Hello, World!");

        return context.publish(new OrderSubmitted(orderId), CancellationToken.none);
    }
}
```

## Filters and Pipelines

The Java client uses specialized filter interfaces to attach middleware to
specific context types. Implement `ConsumeFilter<T>` for consumer pipelines or
`SendFilter<T>` for send pipelines.

```java
class AuditFilter implements ConsumeFilter<SubmitOrder> {
    @Override
    public CompletableFuture<Void> send(ConsumeContext<SubmitOrder> ctx,
                                        Pipe<ConsumeContext<SubmitOrder>> next) {
        System.out.println("Audit " + ctx.getMessage().getOrderId());
        return next.send(ctx);
    }
}
```

Filters are composed into a `TypedPipe` and registered using a `PipeRegistry`
keyed by message and context `Class` tokens:

```java
ConsumeFilter<SubmitOrder> audit = new AuditFilter();
TypedPipe<ConsumeContext<SubmitOrder>> pipe = new TypedPipe<>(List.of(audit));
PipeRegistry registry = new PipeRegistry();
registry.register(SubmitOrder.class, ConsumeContext.class, pipe);

ConsumeContext<SubmitOrder> ctx = /* obtain context */;
registry.dispatch(ctx, SubmitOrder.class);
```

### Divergence from C# and MassTransit

MassTransit and the C# client bind filters using generics, so the compiler
selects the correct pipeline for each message and context. Java's type erasure
prevents that, so the Java client dispatches filters through a runtime registry
indexed by `Class` tokens.

