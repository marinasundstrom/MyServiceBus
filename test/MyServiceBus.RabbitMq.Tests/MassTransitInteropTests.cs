using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using MassTransit;
using RabbitMQ.Client;
using TestApp;
using Testcontainers.RabbitMq;

namespace MyServiceBus.RabbitMq.Tests;

[Collection(RabbitMqInteroperabilityCollection.Name)]
public class MassTransitInteropTests
{
    [Fact]
    public async Task MyServiceBus_producer_delivers_to_MassTransit_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"myservicebus-to-masstransit-{Guid.NewGuid():N}";
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Consumer(() => new MassTransitConsumer(received));
            });
        });

        await bus.StartAsync();
        try
        {
            var transportFactory = CreateTransportFactory(container);
            var serializer = new EnvelopeMessageSerializer();
            var sendContext = new RabbitMqPublishContext([typeof(CrossLanguageMessage)], serializer);
            var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage));
            var sendTransport = await transportFactory.GetSendTransport(new Uri($"exchange:{exchangeName}"));

            await sendTransport.Send(
                new CrossLanguageMessage { Value = "myservicebus-to-masstransit" },
                sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("myservicebus-to-masstransit", message.Value);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [Fact]
    public async Task MassTransit_producer_delivers_to_MyServiceBus_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var transportFactory = CreateTransportFactory(container);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage));
        var queueName = $"masstransit-to-myservicebus-{Guid.NewGuid():N}";
        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = exchangeName,
                Durable = true,
                AutoDelete = false
            },
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);

                return Task.CompletedTask;
            },
            messageType => messageType == MessageUrn.For(typeof(CrossLanguageMessage)));

        await receiveTransport.Start();
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
        });
        await bus.StartAsync();
        try
        {
            await bus.Publish(new CrossLanguageMessage { Value = "masstransit-to-myservicebus" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("masstransit-to-myservicebus", message.Value);
        }
        finally
        {
            await bus.StopAsync();
            await receiveTransport.Stop();
        }
    }

    [Fact]
    public async Task MassTransit_message_is_moved_to_MyServiceBus_skipped_queue_when_unrecognized()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var skipped = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"masstransit-to-myservicebus-skipped-{Guid.NewGuid():N}";
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint($"{queueName}_skipped", endpoint =>
            {
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Consumer(() => new MassTransitConsumer(skipped));
            });
        });

        await bus.StartAsync();
        var receiveTransport = await CreateTransportFactory(container).CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage)),
                Durable = true,
                AutoDelete = false
            },
            _ => throw new InvalidOperationException("An unrecognized message must not reach the handler."),
            _ => false);

        await receiveTransport.Start();
        try
        {
            await bus.Publish(new CrossLanguageMessage { Value = "skipped-by-myservicebus" });

            var message = await skipped.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("skipped-by-myservicebus", message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
            await bus.StopAsync();
        }
    }

    [Fact]
    public async Task MassTransit_message_is_retried_then_moved_to_MyServiceBus_error_queue()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var error = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"masstransit-to-myservicebus-error-{Guid.NewGuid():N}";
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint($"{queueName}_error", endpoint =>
            {
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Consumer(() => new MassTransitConsumer(error));
            });
        });

        await bus.StartAsync();
        var transportFactory = CreateTransportFactory(container);
        var attempts = 0;
        var pipeConfigurator = new PipeConfigurator<ConsumeContext<CrossLanguageMessage>>();
        pipeConfigurator.UseFilter(new ErrorTransportFilter<CrossLanguageMessage>());
        pipeConfigurator.UseRetry(2);
        pipeConfigurator.UseExecute(_ =>
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("retry-exhausted");
        });
        var pipe = pipeConfigurator.Build();
        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage)),
                Durable = true,
                AutoDelete = false
            },
            receiveContext => pipe.Send(new ConsumeContextImpl<CrossLanguageMessage>(
                receiveContext,
                transportFactory,
                new SendPipe(Pipe.Empty<SendContext>()),
                new PublishPipe(Pipe.Empty<PublishContext>()),
                new EnvelopeMessageSerializer(),
                new Uri(container.GetConnectionString()),
                new SendContextFactory(),
                new PublishContextFactory())),
            messageType => messageType == MessageUrn.For(typeof(CrossLanguageMessage)));

        await receiveTransport.Start();
        try
        {
            await bus.Publish(new CrossLanguageMessage { Value = "error-from-myservicebus" });

            var message = await error.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("error-from-myservicebus", message.Value);
            Assert.Equal(3, Volatile.Read(ref attempts));
        }
        finally
        {
            await receiveTransport.Stop();
            await bus.StopAsync();
        }
    }

    [Fact]
    public async Task MyServiceBus_request_client_receives_MassTransit_response()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var observedRequestId = new TaskCompletionSource<Guid>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"myservicebus-request-to-masstransit-{Guid.NewGuid():N}";
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Consumer(() => new MassTransitRequestConsumer(observedRequestId));
            });
        });

        await bus.StartAsync();
        try
        {
            var client = new GenericRequestClient<InteropRequest>(
                CreateTransportFactory(container),
                new EnvelopeMessageSerializer(),
                new SendContextFactory(),
                timeout: MyServiceBus.RequestTimeout.After(TimeSpan.FromSeconds(20)));

            var response = await client.GetResponseAsync<InteropResponse>(
                new InteropRequest { Value = "from-myservicebus" });

            Assert.Equal("response-from-masstransit", response.Message.Value);
            Assert.NotEqual(Guid.Empty, await observedRequestId.Task.WaitAsync(TimeSpan.FromSeconds(20)));
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [Fact]
    public async Task MassTransit_request_client_receives_MyServiceBus_response()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var transportFactory = CreateTransportFactory(container);
        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(InteropRequest));
        var queueName = $"masstransit-request-to-myservicebus-{Guid.NewGuid():N}";
        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = exchangeName,
                Durable = true,
                AutoDelete = false
            },
            async receiveContext =>
            {
                var consumeContext = new ConsumeContextImpl<InteropRequest>(
                    receiveContext,
                    transportFactory,
                    new SendPipe(Pipe.Empty<SendContext>()),
                    new PublishPipe(Pipe.Empty<PublishContext>()),
                    new EnvelopeMessageSerializer(),
                    new Uri(container.GetConnectionString()),
                    new SendContextFactory(),
                    new PublishContextFactory());
                await consumeContext.RespondAsync(
                    new InteropResponse { Value = "response-from-myservicebus" });
            },
            messageType => messageType == MessageUrn.For(typeof(InteropRequest)));

        await receiveTransport.Start();
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
        });
        await bus.StartAsync();
        try
        {
            var client = bus.CreateRequestClient<InteropRequest>(
                MassTransit.RequestTimeout.After(s: 20));
            var response = await client.GetResponse<InteropResponse>(
                new InteropRequest { Value = "from-masstransit" });

            Assert.Equal("response-from-myservicebus", response.Message.Value);
        }
        finally
        {
            await bus.StopAsync();
            await receiveTransport.Stop();
        }
    }

    [Fact]
    public async Task MyServiceBus_request_client_receives_MassTransit_fault()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var queueName = $"myservicebus-request-to-masstransit-fault-{Guid.NewGuid():N}";
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Consumer<MassTransitFaultingConsumer>();
            });
        });

        await bus.StartAsync();
        try
        {
            var client = new GenericRequestClient<InteropRequest>(
                CreateTransportFactory(container),
                new EnvelopeMessageSerializer(),
                new SendContextFactory(),
                timeout: MyServiceBus.RequestTimeout.After(TimeSpan.FromSeconds(20)));

            var exception = await Assert.ThrowsAsync<MyServiceBus.RequestFaultException>(() =>
                client.GetResponseAsync<InteropResponse>(
                    new InteropRequest { Value = "fault-from-masstransit" }));

            Assert.Contains("mass-transit-fault", exception.Fault.Exceptions[0].Message);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [Fact]
    public async Task MassTransit_request_client_receives_MyServiceBus_fault()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var transportFactory = CreateTransportFactory(container);
        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(InteropRequest));
        var queueName = $"masstransit-request-to-myservicebus-fault-{Guid.NewGuid():N}";
        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = exchangeName,
                Durable = true,
                AutoDelete = false
            },
            async receiveContext =>
            {
                var address = receiveContext.ResponseAddress ?? receiveContext.FaultAddress
                    ?? throw new InvalidOperationException("Request did not include a fault or response address.");
                var sendTransport = await transportFactory.GetSendTransport(address);
                var sendContext = new SendContext(
                    MessageTypeCache.GetMessageTypes(typeof(Fault<InteropRequest>)),
                    new EnvelopeMessageSerializer())
                {
                    MessageId = Guid.NewGuid().ToString(),
                    RequestId = receiveContext.RequestId,
                    DestinationAddress = address
                };
                await sendTransport.Send(
                    new Fault<InteropRequest>
                    {
                        Message = new InteropRequest { Value = "fault-from-myservicebus" },
                        FaultId = Guid.NewGuid(),
                        MessageId = receiveContext.MessageId,
                        SentTime = DateTimeOffset.UtcNow,
                        Exceptions =
                        [
                            ExceptionInfo.FromException(
                                new InvalidOperationException("myservicebus-fault"))
                        ]
                    },
                    sendContext);
            },
            messageType => messageType == MessageUrn.For(typeof(InteropRequest)));

        await receiveTransport.Start();
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
        });
        await bus.StartAsync();
        try
        {
            var client = bus.CreateRequestClient<InteropRequest>(
                MassTransit.RequestTimeout.After(s: 20));
            var exception = await Assert.ThrowsAsync<MassTransit.RequestFaultException>(() =>
                client.GetResponse<InteropResponse>(
                    new InteropRequest { Value = "fault-from-myservicebus" }));

            Assert.Contains("myservicebus-fault", exception.Message);
        }
        finally
        {
            await bus.StopAsync();
            await receiveTransport.Stop();
        }
    }

    [CrossLanguageFact]
    public async Task Java_MyServiceBus_request_client_receives_MassTransit_response()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var observedRequestId = new TaskCompletionSource<Guid>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(InteropRequest));
        var queueName = $"java-request-to-masstransit-{Guid.NewGuid():N}";
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Consumer(() => new MassTransitRequestConsumer(observedRequestId));
            });
        });

        await bus.StartAsync();
        try
        {
            using var javaPeer = JavaInteropPeer.Start(
                container, "request", exchangeName, queueName, "from-java", durableExchange: true);
            await JavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromMinutes(2));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
            Assert.NotEqual(Guid.Empty, await observedRequestId.Task.WaitAsync(TimeSpan.FromSeconds(20)));
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [CrossLanguageFact]
    public async Task MassTransit_request_client_receives_Java_MyServiceBus_response()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(InteropRequest));
        var queueName = $"masstransit-request-to-java-{Guid.NewGuid():N}";
        using var javaPeer = JavaInteropPeer.Start(
            container, "respond", exchangeName, queueName, "from-masstransit", durableExchange: true);
        await JavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
        });
        await bus.StartAsync();
        try
        {
            var client = bus.CreateRequestClient<InteropRequest>(
                MassTransit.RequestTimeout.After(s: 20));
            var response = await client.GetResponse<InteropResponse>(
                new InteropRequest { Value = "from-masstransit" });

            Assert.Equal("response-from-java", response.Message.Value);
            await JavaInteropPeer.WaitForOutput(javaPeer, "RESPONDED", TimeSpan.FromSeconds(20));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [CrossLanguageFact]
    public async Task Java_MyServiceBus_request_client_receives_MassTransit_fault()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(InteropRequest));
        var queueName = $"java-request-to-masstransit-fault-{Guid.NewGuid():N}";
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Consumer<MassTransitFaultingConsumer>();
            });
        });

        await bus.StartAsync();
        try
        {
            using var javaPeer = JavaInteropPeer.Start(
                container, "request-fault", exchangeName, queueName, "fault-from-java", durableExchange: true);
            await JavaInteropPeer.WaitForOutput(javaPeer, "FAULT", TimeSpan.FromMinutes(2));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [CrossLanguageFact]
    public async Task MassTransit_request_client_receives_Java_MyServiceBus_fault()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(InteropRequest));
        var queueName = $"masstransit-request-to-java-fault-{Guid.NewGuid():N}";
        using var javaPeer = JavaInteropPeer.Start(
            container, "fault", exchangeName, queueName, "fault-from-masstransit", durableExchange: true);
        await JavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
        });
        await bus.StartAsync();
        try
        {
            var client = bus.CreateRequestClient<InteropRequest>(
                MassTransit.RequestTimeout.After(s: 40));
            var responseTask = client.GetResponse<InteropResponse>(
                new InteropRequest { Value = "fault-from-masstransit" });
            await JavaInteropPeer.WaitForOutput(javaPeer, "FAULTED", TimeSpan.FromSeconds(20));
            var exception = await Assert.ThrowsAsync<MassTransit.RequestFaultException>(() =>
                responseTask);

            Assert.Contains("java-fault", exception.Message);
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [CrossLanguageFact]
    public async Task Java_MyServiceBus_producer_delivers_to_MassTransit_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"java-to-masstransit-{Guid.NewGuid():N}";
        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage));
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Consumer(() => new MassTransitConsumer(received));
            });
        });

        await bus.StartAsync();
        try
        {
            using var javaPeer = JavaInteropPeer.Start(
                container, "produce", exchangeName, queueName, "java-to-masstransit", durableExchange: true);
            await JavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
            Assert.Equal("java-to-masstransit", message.Value);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [CrossLanguageFact]
    public async Task MassTransit_producer_delivers_to_Java_MyServiceBus_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage));
        var queueName = $"masstransit-to-java-{Guid.NewGuid():N}";
        using var javaPeer = JavaInteropPeer.Start(
            container, "consume", exchangeName, queueName, "masstransit-to-java");
        await JavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
        });
        await bus.StartAsync();
        try
        {
            await bus.Publish(new CrossLanguageMessage { Value = "masstransit-to-java" });
            await JavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    private static RabbitMqTransportFactory CreateTransportFactory(RabbitMqContainer container)
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(container.GetConnectionString())
        };
        return new RabbitMqTransportFactory(
            new ConnectionProvider(connectionFactory),
            new RabbitMqFactoryConfigurator());
    }

    private sealed class MassTransitConsumer : MassTransit.IConsumer<CrossLanguageMessage>
    {
        private readonly TaskCompletionSource<CrossLanguageMessage> received;

        public MassTransitConsumer(TaskCompletionSource<CrossLanguageMessage> received)
        {
            this.received = received;
        }

        public Task Consume(MassTransit.ConsumeContext<CrossLanguageMessage> context)
        {
            received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    private sealed class MassTransitRequestConsumer : MassTransit.IConsumer<InteropRequest>
    {
        private readonly TaskCompletionSource<Guid> observedRequestId;

        public MassTransitRequestConsumer(TaskCompletionSource<Guid> observedRequestId)
        {
            this.observedRequestId = observedRequestId;
        }

        public async Task Consume(MassTransit.ConsumeContext<InteropRequest> context)
        {
            if (context.RequestId is { } requestId)
                observedRequestId.TrySetResult(requestId);

            await context.RespondAsync(
                new InteropResponse { Value = "response-from-masstransit" });
        }
    }

    private sealed class MassTransitFaultingConsumer : MassTransit.IConsumer<InteropRequest>
    {
        public Task Consume(MassTransit.ConsumeContext<InteropRequest> context)
        {
            throw new InvalidOperationException("mass-transit-fault");
        }
    }
}
