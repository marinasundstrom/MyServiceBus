package com.myservicebus.persistence.postgresql;

import com.myservicebus.persistence.OutboxSession;
import java.sql.Connection;
import java.util.Objects;

public final class PostgreSqlOutboxSession {
    private PostgreSqlOutboxSession() {
    }

    /**
     * Captures scoped publish and send operations in the caller-owned PostgreSQL transaction.
     *
     * @throws IllegalStateException when an outbox transaction is already active in this service scope
     */
    public static OutboxSession.Registration useTransaction(
            OutboxSession session, Connection connection, String serviceName) {
        Objects.requireNonNull(session, "session");
        return session.begin(new PostgreSqlOutboxWriter(connection, serviceName));
    }
}
