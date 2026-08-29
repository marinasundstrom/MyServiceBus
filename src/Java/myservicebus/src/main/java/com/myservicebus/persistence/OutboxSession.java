package com.myservicebus.persistence;

import java.util.Objects;

public final class OutboxSession {
    private OutboxWriter writer;

    /**
     * Activates a transactional writer for scoped publish and send endpoints until the registration is closed.
     *
     * @throws IllegalStateException when an outbox transaction is already active in this service scope
     */
    public synchronized Registration begin(OutboxWriter outboxWriter) {
        Objects.requireNonNull(outboxWriter, "outboxWriter");
        if (writer != null) {
            throw new IllegalStateException("An outbox transaction is already active in this service scope.");
        }
        writer = outboxWriter;
        return new Registration(this, outboxWriter);
    }

    public synchronized OutboxWriter getWriter() {
        return writer;
    }

    public static final class Registration implements AutoCloseable {
        private OutboxSession session;
        private final OutboxWriter writer;

        private Registration(OutboxSession session, OutboxWriter writer) {
            this.session = session;
            this.writer = writer;
        }

        @Override
        public synchronized void close() {
            if (session != null) {
                session.clear(writer);
                session = null;
            }
        }
    }

    private synchronized void clear(OutboxWriter expected) {
        if (writer == expected) {
            writer = null;
        }
    }
}
