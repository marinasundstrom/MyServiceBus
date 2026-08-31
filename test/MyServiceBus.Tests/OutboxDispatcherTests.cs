namespace MyServiceBus.Tests;

using MyServiceBus.Persistence;

public class OutboxDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Persisted_envelope_copies_mutable_input()
    {
        var messageTypes = new[] { "urn:message:Contracts:OrderSubmitted" };
        var body = new byte[] { 1, 2, 3 };
        var headers = new Dictionary<string, string> { ["traceparent"] = "original" };

        var message = new OutboxMessage(
            Guid.NewGuid(), Guid.NewGuid(), OutboxDeliveryIntent.Publish,
            new Uri("rabbitmq://localhost/exchange/orders"), messageTypes, body,
            "application/vnd.masstransit+json", headers, Now);
        messageTypes[0] = "changed";
        body[0] = 9;
        headers["traceparent"] = "changed";

        Assert.Equal("urn:message:Contracts:OrderSubmitted", message.MessageTypes[0]);
        Assert.Equal((byte)1, message.Body.Span[0]);
        Assert.Equal("original", message.Headers["traceparent"]);
    }

    [Fact]
    public async Task Dispatches_persisted_identity_and_marks_owned_lease()
    {
        var message = CreateMessage();
        var store = new TestOutboxStore(new OutboxLease(message, "replica-a", Now.AddMinutes(1), 0));
        var transport = new CapturingTransport();
        var dispatcher = CreateDispatcher(store, transport);

        var result = await dispatcher.DispatchBatchAsync(Request());

        Assert.Same(message, transport.Message);
        Assert.Equal(message.MessageId, transport.Message!.MessageId);
        Assert.Equal(message.RecordId, store.MarkedRecordId);
        Assert.Equal("replica-a", store.MarkedOwnerId);
        Assert.Equal(new OutboxDispatchBatchResult(1, 1, 0, 0), result);
    }

    [Fact]
    public async Task Failed_dispatch_is_rescheduled_without_replacing_identity()
    {
        var message = CreateMessage();
        var store = new TestOutboxStore(new OutboxLease(message, "replica-a", Now.AddMinutes(1), 2));
        var transport = new CapturingTransport(new IOException("broker unavailable"));
        var dispatcher = CreateDispatcher(store, transport);

        var result = await dispatcher.DispatchBatchAsync(Request());

        Assert.Equal(message.MessageId, transport.Message!.MessageId);
        Assert.Equal(message.RecordId, store.RescheduledRecordId);
        Assert.Equal(Now.AddSeconds(4), store.NextAttemptAtUtc);
        Assert.Equal(nameof(IOException), store.FailureCategory);
        Assert.Equal(new OutboxDispatchBatchResult(1, 0, 1, 0), result);
    }

    [Fact]
    public async Task Reports_lease_lost_after_broker_acceptance()
    {
        var message = CreateMessage();
        var store = new TestOutboxStore(new OutboxLease(message, "replica-a", Now.AddMinutes(1), 0))
        {
            OwnsLease = false
        };
        var dispatcher = CreateDispatcher(store, new CapturingTransport());

        var result = await dispatcher.DispatchBatchAsync(Request());

        Assert.Equal(new OutboxDispatchBatchResult(1, 0, 0, 1), result);
    }

    [Fact]
    public async Task Transport_dispatcher_sends_stored_body_and_identity_without_reserializing()
    {
        var correlationId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var responseAddress = new Uri("queue:responses");
        var message = new OutboxMessage(
            Guid.NewGuid(), Guid.NewGuid(), OutboxDeliveryIntent.Publish,
            new Uri("exchange:orders"), ["urn:message:Contracts:OrderSubmitted"], [1, 2, 3],
            "application/vnd.masstransit+json", new Dictionary<string, string> { ["traceparent"] = "00-test" },
            Now, correlationId: correlationId, conversationId: conversationId, responseAddress: responseAddress);
        var factory = new CapturingTransportFactory();
        var hooks = new CapturingHookDispatcher();

        await new TransportOutboxDispatcher(factory, hooks).DispatchAsync(message);

        Assert.Equal(message.DestinationAddress, factory.Address);
        Assert.Equal(message.Body.ToArray(), factory.Transport.Body);
        Assert.Equal(message.ContentType, factory.Transport.ContentType);
        Assert.Equal(message.MessageId.ToString(), factory.Transport.Context!.MessageId);
        Assert.Equal(correlationId.ToString(), factory.Transport.Context.CorrelationId);
        Assert.Equal(message.MessageId.ToString(), factory.Transport.Context.Headers["_message_id"]);
        Assert.Equal(responseAddress.ToString(), factory.Transport.Context.Headers["_reply_to"]);
        Assert.Equal("00-test", factory.Transport.Context.Headers["traceparent"]);
        var operation = Assert.IsType<MessageOperationHookEvent>(Assert.Single(hooks.Events));
        Assert.Equal("published", operation.Kind);
        Assert.True(operation.Succeeded);
        Assert.Equal("Contracts.OrderSubmitted", operation.MessageType);
        Assert.Equal(message.MessageTypes[0], operation.MessageUrn);
        Assert.Equal(message.DestinationAddress.ToString(), operation.DestinationAddress);
        Assert.Equal(correlationId.ToString(), operation.CorrelationId);
        Assert.Equal(conversationId.ToString(), operation.ConversationId);
    }

    private static OutboxDispatcher CreateDispatcher(TestOutboxStore store, CapturingTransport transport) =>
        new(
            store,
            transport,
            new ExponentialOutboxRetryPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)),
            new FixedTimeProvider(Now));

    private static OutboxLeaseRequest Request() => new("replica-a", 10, Now, TimeSpan.FromMinutes(1));

    private static OutboxMessage CreateMessage() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        OutboxDeliveryIntent.Publish,
        new Uri("rabbitmq://localhost/exchange/orders"),
        ["urn:message:Contracts:OrderSubmitted"],
        [1, 2, 3],
        "application/vnd.masstransit+json",
        new Dictionary<string, string> { ["traceparent"] = "00-test" },
        Now);

    private sealed class CapturingTransport : IOutboxTransportDispatcher
    {
        private readonly Exception? exception;

        public CapturingTransport(Exception? exception = null)
        {
            this.exception = exception;
        }

        public OutboxMessage? Message { get; private set; }

        public Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            Message = message;
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CapturingTransportFactory : ITransportFactory
    {
        public CapturingSendTransport Transport { get; } = new();
        public Uri? Address { get; private set; }

        public Task<ISendTransport> GetSendTransport(
            Uri address,
            CancellationToken cancellationToken = default)
        {
            Address = address;
            return Task.FromResult<ISendTransport>(Transport);
        }
    }

    private sealed class CapturingHookDispatcher : IBusHookDispatcher
    {
        public bool IsEnabled => true;
        public List<BusHookEvent> Events { get; } = [];

        public void Dispatch(BusHookEvent busEvent) => Events.Add(busEvent);
    }

    private sealed class CapturingSendTransport : ISendTransport
    {
        public byte[]? Body { get; private set; }
        public string? ContentType { get; private set; }
        public SendContext? Context { get; private set; }

        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
            where T : class
        {
            Body = context.GetMessageBody(message).GetBytes();
            ContentType = context.Headers["_content_type"].ToString();
            Context = context;
            return Task.CompletedTask;
        }
    }

    private sealed class TestOutboxStore : IOutboxStore
    {
        private readonly IReadOnlyList<OutboxLease> leases;

        public TestOutboxStore(params OutboxLease[] leases)
        {
            this.leases = leases;
        }

        public bool OwnsLease { get; init; } = true;
        public Guid? MarkedRecordId { get; private set; }
        public string? MarkedOwnerId { get; private set; }
        public Guid? RescheduledRecordId { get; private set; }
        public DateTimeOffset? NextAttemptAtUtc { get; private set; }
        public string? FailureCategory { get; private set; }

        public Task<IReadOnlyList<OutboxLease>> LeaseAsync(
            OutboxLeaseRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(leases);

        public Task<bool> MarkDispatchedAsync(
            Guid recordId,
            string ownerId,
            DateTimeOffset dispatchedAtUtc,
            CancellationToken cancellationToken = default)
        {
            MarkedRecordId = recordId;
            MarkedOwnerId = ownerId;
            return Task.FromResult(OwnsLease);
        }

        public Task<bool> RescheduleAsync(
            Guid recordId,
            string ownerId,
            DateTimeOffset nextAttemptAtUtc,
            string failureCategory,
            CancellationToken cancellationToken = default)
        {
            RescheduledRecordId = recordId;
            NextAttemptAtUtc = nextAttemptAtUtc;
            FailureCategory = failureCategory;
            return Task.FromResult(OwnsLease);
        }

        public Task<ScheduleCancellationResult> CancelScheduledAsync(
            Guid messageId,
            DateTimeOffset cancelledAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ScheduleCancellationResult.NotFound);
    }
}
