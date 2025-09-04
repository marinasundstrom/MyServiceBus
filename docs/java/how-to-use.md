# How to use

The Service Bus API is still in the works.

## Configure Service Bus

We use `ServiceCollection` to register services that will supplied to the `Consumers`.

```java
package com.myservicebus.testapp;

import java.util.UUID;

import com.myservicebus.MyService;
import com.myservicebus.MyServiceImpl;
import com.myservicebus.ServiceBus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.rabbitmq.RabbitMqBusFactory;

public class Main {
    public static void main(String[] args) throws Exception {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(MyService.class, MyServiceImpl.class);

        RabbitMqBusFactory.configure(services, x -> {
            x.addConsumer(SubmitOrderConsumer.class);
        }, (context, cfg) -> {
            cfg.host("rabbitmq://localhost");

            cfg.receiveEndpoint("submit-order-queue", e -> {
                e.configureConsumer(context, SubmitOrderConsumer.class);
            });
        });

        ServiceProvider provider = services.buildServiceProvider();
        ServiceBus bus = provider.getService(ServiceBus.class);

        bus.start();

        System.out.println("Up and running");

        SubmitOrder message = new SubmitOrder(UUID.randomUUID());

        bus.publish(message);

        System.out.println("Waiting");

        System.in.read();
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

If you are using Maven:

```xml
<!-- Lombok -->
<dependency>
    <groupId>org.projectlombok</groupId>
    <artifactId>lombok</artifactId>
    <version>1.18.30</version>
    <scope>provided</scope>
</dependency>
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