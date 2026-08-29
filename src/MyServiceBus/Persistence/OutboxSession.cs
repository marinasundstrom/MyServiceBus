namespace MyServiceBus.Persistence;

public sealed class OutboxSession
{
    private IOutboxWriter? writer;

    internal IOutboxWriter? Writer => Volatile.Read(ref writer);

    /// <summary>
    /// Activates the supplied transactional writer for scoped publish and send endpoints until the returned
    /// registration is disposed.
    /// </summary>
    /// <exception cref="InvalidOperationException">An outbox transaction is already active in this service scope.</exception>
    public IDisposable Begin(IOutboxWriter outboxWriter)
    {
        ArgumentNullException.ThrowIfNull(outboxWriter);
        if (Interlocked.CompareExchange(ref writer, outboxWriter, null) is not null)
            throw new InvalidOperationException("An outbox transaction is already active in this service scope.");
        return new Registration(this, outboxWriter);
    }

    private sealed class Registration : IDisposable
    {
        private OutboxSession? session;
        private readonly IOutboxWriter writer;

        public Registration(OutboxSession session, IOutboxWriter writer)
        {
            this.session = session;
            this.writer = writer;
        }

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref session, null);
            if (current is not null)
                Interlocked.CompareExchange(ref current.writer, null, writer);
        }
    }
}
