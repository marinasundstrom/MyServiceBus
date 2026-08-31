package com.myservicebus.testapp;

import io.javalin.Javalin;
import java.net.URI;
import java.time.Duration;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;

import com.myservicebus.ExceptionInfo;
import com.myservicebus.Fault;
import com.myservicebus.ScopedClientFactory;
import com.myservicebus.Response;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.MessageScheduler;
import com.myservicebus.JobClient;
import com.myservicebus.RecurringJobDefinition;
import com.myservicebus.RecurringJobIdentity;
import com.myservicebus.RecurringJobScheduler;
import com.myservicebus.FixedIntervalRecurringJobCadence;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.inspection.InspectionServices;
import com.myservicebus.monitoring.MonitoringExporter;
import com.myservicebus.monitoring.MonitoringExporterOptions;
import com.myservicebus.monitoring.MonitoringServices;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import com.myservicebus.logging.LogLevel;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Logging;
import com.myservicebus.generated.GeneratedConsumerCatalog;

public class Main {
    public static void main(String[] args) {
        ServiceCollection services = ServiceCollection.create();

        // Configure logging provider Slf4j
        services.from(Logging.class)
                .addLogging(builder -> builder.addSlf4j(cfg -> {
                    // cfg.setMinimumLevel(LogLevel.WARN);
                    cfg.setLevel("com.myservicebus", LogLevel.DEBUG);
                }));

        services.from(InspectionServices.class)
                .addInspection();

        MonitoringExporterOptions monitoringOptions = new MonitoringExporterOptions();
        monitoringOptions.setServiceAddress(URI.create(
                System.getenv().getOrDefault("MONITORING_SERVICE_URL", "http://localhost:5310")));
        monitoringOptions.setApplicationName("TestApp.Java");
        monitoringOptions.getLabels().put("group", "sample-system");
        monitoringOptions.getLabels().put("environment",
                System.getenv().getOrDefault("ENVIRONMENT", "Development"));
        monitoringOptions.getLabels().put("role", "worker");
        MonitoringExporter monitoringExporter = MonitoringServices.addMonitoring(services, monitoringOptions);

        String rabbitMqHost = System.getenv().getOrDefault("RABBITMQ_HOST", "localhost");
        int rabbitMqPort = Integer.parseInt(System.getenv().getOrDefault("RABBITMQ_PORT", "5672"));

        services.from(MessageBusServices.class)
                .addServiceBus(c -> {
                    GeneratedConsumerCatalog.INSTANCE.register(c);
                    c.addJobConsumer(DemoTrackedJobConsumer.class, DemoTrackedJob.class, options -> options
                            .setJobTypeName("sample-report")
                            .setConcurrentJobLimit(2)
                            .setRetry(retry -> retry.interval(1, Duration.ofSeconds(1))));

                    c.using(RabbitMqFactoryConfigurator.class, (context, cfg) -> {
                        cfg.host(rabbitMqHost, rabbitMqPort, h -> {
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
        AtomicBoolean started = new AtomicBoolean();

        try {
            serviceBus.start();
            monitoringExporter.start(provider);
            RecurringJobScheduler recurringJobs = provider.getRequiredService(RecurringJobScheduler.class);
            RecurringJobIdentity sampleReportIdentity = new RecurringJobIdentity("sample-report", "aspire-demo");
            recurringJobs.addOrUpdate(
                    new RecurringJobDefinition(
                            sampleReportIdentity,
                            new FixedIntervalRecurringJobCadence(Duration.ofMinutes(5)),
                            "Creates a small tracked report job so recurring definitions and their executions can be observed together.",
                            null,
                            null,
                            com.myservicebus.RecurringJobMisfirePolicy.FIRE_ONCE_NOW,
                            1,
                            com.myservicebus.RecurringJobOverlapPolicy.ALLOW),
                    new DemoTrackedJob("recurring-sample", false, false)).toCompletableFuture().join();
            recurringJobs.triggerNow(sampleReportIdentity).toCompletableFuture().join();
            started.set(true);
            logger.info("🚀 Test app started");
        } catch (Exception e) {
            logger.error("❌ Failed to start service bus", e);
            return;
        }

        int httpPort = Integer.parseInt(System.getenv().getOrDefault("HTTP_PORT", "5301"));
        var app = Javalin.create().start(httpPort);
        app.get("/health/live", ctx -> ctx.status(200));
        app.get("/health/ready", ctx -> ctx.status(started.get() ? 200 : 503));

        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            started.set(false);
            monitoringExporter.close();
            try {
                serviceBus.stop();
            } catch (Exception exception) {
                logger.error("Failed to stop service bus", exception);
            }
        }));

        app.get("/publish", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var publishEndpoint = scope.getServiceProvider().getService(PublishEndpoint.class);
                SubmitOrder message = new SubmitOrder(UUID.randomUUID(), DemoScenario.createSubmitMessage("java", false));
                try {
                    publishEndpoint.publish(message).join();
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
                    publishEndpoint.publish(message).join();
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
                    sendEndpoint.send(message).join();
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
                    sendEndpoint.send(message).join();
                    logger.info("📤 Sent fault SubmitOrder {} ✅", message.getOrderId());
                    ctx.result("Sent fault SubmitOrder");
                } catch (Exception e) {
                    logger.error("❌ Failed to send fault message", e);
                    ctx.status(500).result("Failed to send fault message");
                }
            }
        });

        app.post("/schedule", ctx -> {
            int requestedDelay = ctx.queryParamAsClass("delaySeconds", Integer.class).getOrDefault(120);
            int delaySeconds = Math.max(5, Math.min(requestedDelay, 3_600));
            try (ServiceScope scope = provider.createScope()) {
                var scheduler = scope.getServiceProvider().getRequiredService(MessageScheduler.class);
                var message = new SubmitOrder(
                        UUID.randomUUID(), DemoScenario.createSubmitMessage("java-scheduled", false));
                var handle = scheduler.schedulePublish(message, Duration.ofSeconds(delaySeconds))
                        .toCompletableFuture().join();
                ctx.status(202).json(java.util.Map.of(
                        "tokenId", handle.getTokenId().toString(),
                        "dueAtUtc", handle.getScheduledTime().toString(),
                        "messageType", SubmitOrder.class.getSimpleName()));
            }
        });

        app.delete("/schedule/{tokenId}", ctx -> {
            UUID tokenId = UUID.fromString(ctx.pathParam("tokenId"));
            try (ServiceScope scope = provider.createScope()) {
                var scheduler = scope.getServiceProvider().getRequiredService(MessageScheduler.class);
                var status = scheduler.cancelScheduledPublish(tokenId).toCompletableFuture().join();
                ctx.json(java.util.Map.of("tokenId", tokenId.toString(), "status", status.toString()));
            }
        });

        app.post("/jobs", ctx -> {
            int requestedDelay = ctx.queryParamAsClass("delaySeconds", Integer.class).getOrDefault(0);
            int delaySeconds = Math.max(0, Math.min(requestedDelay, 3_600));
            boolean failFirstAttempt = ctx.queryParamAsClass("failFirstAttempt", Boolean.class).getOrDefault(false);
            boolean failAlways = ctx.queryParamAsClass("failAlways", Boolean.class).getOrDefault(false);
            JobClient jobs = provider.getRequiredService(JobClient.class);
            var job = new DemoTrackedJob(
                    "report-" + java.time.Instant.now().toString(),
                    failFirstAttempt,
                    failAlways);
            var receipt = delaySeconds == 0
                    ? jobs.submit(job).toCompletableFuture().join()
                    : jobs.schedule(java.time.Instant.now().plusSeconds(delaySeconds), job).toCompletableFuture().join();
            ctx.status(202).json(receipt);
        });

        app.get("/request", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var requestClientFactory = scopedSp.getService(ScopedClientFactory.class);
                var requestClient = requestClientFactory.create(TestRequest.class);
                try {
                    var response = requestClient
                            .getResponse(new TestRequest(DemoScenario.createRequestMessage("java", false)), TestResponse.class)
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
                            .getResponse(new TestRequest(DemoScenario.createRequestMessage("java", true)), TestResponse.class)
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
                                    Fault.class)
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
                                    Fault.class)
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
