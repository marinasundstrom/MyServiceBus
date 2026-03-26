package com.myservicebus.testapp;

import io.javalin.Javalin;
import java.util.UUID;

import com.myservicebus.ExceptionInfo;
import com.myservicebus.Fault;
import com.myservicebus.ScopedClientFactory;
import com.myservicebus.Response;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.logging.LogLevel;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Logging;
import com.myservicebus.testapp.dashboard.DashboardApi;
import com.myservicebus.testapp.dashboard.DashboardMetricsFilters;
import com.myservicebus.testapp.dashboard.DashboardMetadata;
import com.myservicebus.testapp.dashboard.DashboardState;
import java.time.Instant;

public class Main {
    public static void main(String[] args) {
        DashboardState inspectionState = new DashboardState();

        ServiceCollection services = ServiceCollection.create();

        // Configure logging provider Slf4j
        services.from(Logging.class)
                .addLogging(builder -> builder.addSlf4j(cfg -> {
                    // cfg.setMinimumLevel(LogLevel.WARN);
                    cfg.setLevel("com.myservicebus", LogLevel.DEBUG);
                }));

        String rabbitMqHost = System.getenv().getOrDefault("RABBITMQ_HOST", "localhost");

        services.from(MessageBusServices.class)
                .addServiceBus(c -> {
                    c.configureSend(cfg -> cfg.useFilter(new DashboardMetricsFilters.SendMetricsFilter(inspectionState)));
                    c.configurePublish(cfg -> cfg.useFilter(new DashboardMetricsFilters.PublishMetricsFilter(inspectionState)));
                    c.addConsumer(SubmitOrderConsumer.class, SubmitOrder.class,
                            cfg -> cfg.useFilter(new DashboardMetricsFilters.ConsumeMetricsFilter<>(inspectionState, "submit-order", SubmitOrder.class)));
                    c.addConsumer(OrderSubmittedConsumer.class, OrderSubmitted.class,
                            cfg -> cfg.useFilter(new DashboardMetricsFilters.ConsumeMetricsFilter<>(inspectionState, "order-submitted", OrderSubmitted.class)));
                    c.addConsumer(TestRequestConsumer.class, TestRequest.class,
                            cfg -> cfg.useFilter(new DashboardMetricsFilters.ConsumeMetricsFilter<>(inspectionState, "test-request", TestRequest.class)));
                    c.addConsumer(SubmitOrderFaultConsumer.class);

                    c.using(RabbitMqFactoryConfigurator.class, (context, cfg) -> {
                        cfg.host(rabbitMqHost, h -> {
                            h.username("guest");
                            h.password("guest");
                        });

                        // Fault<T> consumers don't auto-bind; listen on the queue suffixed with
                        // `_fault`
                        // for the original endpoint. SubmitOrderFaultConsumer handles
                        // Fault<SubmitOrder>
                        // messages published to `submit-order_fault`.
                        cfg.receiveEndpoint("submit-order_fault", e -> {
                            e.configureConsumer(context, SubmitOrderFaultConsumer.class);

                            /*
                             * e.handler(Fault<SubmitOrder>.class, ctx -> {
                             * var fault = ctx.getMessage();
                             * var msg = fault.getMessage();
                             * System.out.println(msg.getOrderId());
                             * // inspect or process the fault
                             * return CompletableFuture.completedFuture(null);
                             * });
                             */
                        });

                        cfg.configureEndpoints(context);
                    });
                });

        ServiceProvider provider = services.buildServiceProvider();
        LoggerFactory loggerFactory = provider.getService(LoggerFactory.class);
        final Logger logger = loggerFactory != null ? loggerFactory.create(Main.class) : null;
        MessageBus serviceBus = provider.getRequiredService(MessageBus.class);

        try {
            serviceBus.start();
            inspectionState.markStarted(Instant.now());
            logger.info("🚀 Test app started");
        } catch (Exception e) {
            logger.error("❌ Failed to start service bus", e);
            return;
        }

        int httpPort = Integer.parseInt(System.getenv().getOrDefault("HTTP_PORT", "5301"));
        var app = Javalin.create().start(httpPort);
        app.get("/health/live", ctx -> ctx.status(200));
        app.get("/health/ready", ctx -> ctx.status(inspectionState.isStarted() ? 200 : 503));
        DashboardApi.register(app, serviceBus, new DashboardMetadata("TestApp", "rabbitmq"), inspectionState);

        app.get("/publish", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var publishEndpoint = scope.getServiceProvider().getService(PublishEndpoint.class);
                SubmitOrder message = new SubmitOrder(UUID.randomUUID(), DemoScenario.createSubmitMessage("java", false));
                try {
                    publishEndpoint.publish(message, CancellationToken.none).join();
                    logger.info("📤 Published SubmitOrder {} ✅", message.getOrderId());
                    ctx.result("Published SubmitOrder");
                } catch (Exception e) {
                    logger.error("❌ Failed to publish message", e);
                    ctx.status(500).result("Failed to publish message");
                }
            }
        });

        app.get("/publish/fault", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var publishEndpoint = scope.getServiceProvider().getService(PublishEndpoint.class);
                SubmitOrder message = new SubmitOrder(UUID.randomUUID(), DemoScenario.createSubmitMessage("java", true));
                try {
                    publishEndpoint.publish(message, CancellationToken.none).join();
                    logger.info("📤 Published fault SubmitOrder {} ✅", message.getOrderId());
                    ctx.result("Published fault SubmitOrder");
                } catch (Exception e) {
                    logger.error("❌ Failed to publish fault message", e);
                    ctx.status(500).result("Failed to publish fault message");
                }
            }
        });

        app.get("/send", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var sendEndpointProvider = scopedSp.getService(SendEndpointProvider.class);
                var sendEndpoint = sendEndpointProvider.getSendEndpoint("rabbitmq://localhost/submit-order");
                SubmitOrder message = new SubmitOrder(UUID.randomUUID(), DemoScenario.createSubmitMessage("java", false));
                try {
                    sendEndpoint.send(message, CancellationToken.none).join();
                    logger.info("📤 Sent SubmitOrder {} ✅", message.getOrderId());
                    ctx.result("Sent SubmitOrder");
                } catch (Exception e) {
                    logger.error("❌ Failed to send message", e);
                    ctx.status(500).result("Failed to send message");
                }
            }
        });

        app.get("/send/fault", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var sendEndpointProvider = scopedSp.getService(SendEndpointProvider.class);
                var sendEndpoint = sendEndpointProvider.getSendEndpoint("rabbitmq://localhost/submit-order");
                SubmitOrder message = new SubmitOrder(UUID.randomUUID(), DemoScenario.createSubmitMessage("java", true));
                try {
                    sendEndpoint.send(message, CancellationToken.none).join();
                    logger.info("📤 Sent fault SubmitOrder {} ✅", message.getOrderId());
                    ctx.result("Sent fault SubmitOrder");
                } catch (Exception e) {
                    logger.error("❌ Failed to send fault message", e);
                    ctx.status(500).result("Failed to send fault message");
                }
            }
        });

        app.get("/request", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var requestClientFactory = scopedSp.getService(ScopedClientFactory.class);
                var requestClient = requestClientFactory.create(TestRequest.class);
                try {
                    var response = requestClient
                            .getResponse(new TestRequest(DemoScenario.createRequestMessage("java", false)), TestResponse.class, CancellationToken.none)
                            .get();
                    logger.info("📨 Received response {} ✅", response.getMessage().toString());
                    ctx.result(response.getMessage().toString());
                } catch (Exception exc) {
                    logger.error("❌ Failed to get response", exc);
                    ctx.result(exc.getMessage().toString());
                }
            }
        });

        app.get("/request/fault", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var requestClientFactory = scopedSp.getService(ScopedClientFactory.class);
                var requestClient = requestClientFactory.create(TestRequest.class);
                try {
                    var response = requestClient
                            .getResponse(new TestRequest(DemoScenario.createRequestMessage("java", true)), TestResponse.class, CancellationToken.none)
                            .get();
                    logger.info("📨 Received response {} ✅", response.getMessage().toString());
                    ctx.result(response.getMessage().toString());
                } catch (Exception exc) {
                    logger.error("❌ Failed to get response", exc);
                    ctx.status(500).result(exc.getMessage());
                }
            }
        });

        app.get("/request_multi", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var requestClientFactory = scopedSp.getService(ScopedClientFactory.class);
                var requestClient = requestClientFactory.create(TestRequest.class);
                try {
                    var response = requestClient
                            .getResponse(new TestRequest(DemoScenario.createRequestMessage("java", false)), TestResponse.class,
                                    Fault.class, CancellationToken.none)
                            .get();

                    response.as(TestResponse.class).ifPresent((Response<TestResponse> r) -> {
                        logger.info("📨 Received response {} ✅", r.getMessage().toString());
                        ctx.result(r.getMessage().toString());
                    });

                    response.as(Fault.class).ifPresent(r -> {
                        var exception = (ExceptionInfo) r.getMessage().getExceptions().get(0);
                        String message = exception.getMessage();
                        if (message == null) {
                            message = exception.toString();
                        }
                        logger.error("❌ Fault received: " + message);
                        ctx.status(500).result(message);
                    });
                } catch (Exception e) {
                    logger.error("❌ Failed to get response", e);
                    ctx.status(500).result("Failed to get response: " + e.getMessage());
                }
            }
        });

        app.get("/request_multi/fault", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var requestClientFactory = scopedSp.getService(ScopedClientFactory.class);
                var requestClient = requestClientFactory.create(TestRequest.class);
                try {
                    var response = requestClient
                            .getResponse(new TestRequest(DemoScenario.createRequestMessage("java", true)), TestResponse.class,
                                    Fault.class, CancellationToken.none)
                            .get();

                    response.as(TestResponse.class).ifPresent((Response<TestResponse> r) -> {
                        logger.info("📨 Received response {} ✅", r.getMessage().toString());
                        ctx.result(r.getMessage().toString());
                    });

                    response.as(Fault.class).ifPresent(r -> {
                        var exception = (ExceptionInfo) r.getMessage().getExceptions().get(0);
                        String message = exception.getMessage();
                        if (message == null) {
                            message = exception.toString();
                        }
                        logger.error("❌ Fault received: " + message);
                        ctx.status(500).result(message);
                    });
                } catch (Exception e) {
                    logger.error("❌ Failed to get response", e);
                    ctx.status(500).result("Failed to get response: " + e.getMessage());
                }
            }
        });

        logger.info("✅ Up and running");
    }
}
