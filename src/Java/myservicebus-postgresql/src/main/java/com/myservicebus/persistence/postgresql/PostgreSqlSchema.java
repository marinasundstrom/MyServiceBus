package com.myservicebus.persistence.postgresql;

import java.sql.Connection;
import java.sql.SQLException;
import java.sql.Statement;
import java.util.Objects;
import javax.sql.DataSource;

public final class PostgreSqlSchema {
    public static final int CURRENT_VERSION = 3;

    private PostgreSqlSchema() {
    }

    public static void ensureCreated(DataSource dataSource) throws SQLException {
        Objects.requireNonNull(dataSource, "dataSource");
        try (Connection connection = dataSource.getConnection()) {
            boolean previousAutoCommit = connection.getAutoCommit();
            connection.setAutoCommit(false);
            try (Statement statement = connection.createStatement()) {
                statement.execute(MIGRATION_SQL);
                connection.commit();
            } catch (SQLException failure) {
                connection.rollback();
                throw failure;
            } finally {
                connection.setAutoCommit(previousAutoCommit);
            }
        }
    }

    static final String MIGRATION_SQL = """
            SELECT pg_advisory_xact_lock(hashtext('myservicebus.persistence.schema'));

            CREATE SCHEMA IF NOT EXISTS myservicebus;

            CREATE TABLE IF NOT EXISTS myservicebus.schema_version (
                singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
                version integer NOT NULL,
                installed_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            INSERT INTO myservicebus.schema_version (singleton, version)
            VALUES (true, 3)
            ON CONFLICT (singleton) DO NOTHING;

            DO $migration$
            BEGIN
                IF (SELECT version FROM myservicebus.schema_version WHERE singleton) NOT IN (2, 3) THEN
                    RAISE EXCEPTION 'Unsupported MyServiceBus PostgreSQL schema version';
                END IF;
            END
            $migration$;

            CREATE TABLE IF NOT EXISTS myservicebus.outbox_message (
                record_id uuid PRIMARY KEY,
                service_name text NOT NULL CHECK (length(service_name) > 0),
                message_id uuid NOT NULL UNIQUE,
                intent smallint NOT NULL CHECK (intent BETWEEN 0 AND 3),
                destination_address text NOT NULL,
                message_types text[] NOT NULL CHECK (cardinality(message_types) > 0),
                body bytea NOT NULL,
                content_type text NOT NULL,
                headers jsonb NOT NULL DEFAULT '{}'::jsonb,
                created_at_utc timestamptz NOT NULL,
                request_id uuid NULL,
                correlation_id uuid NULL,
                conversation_id uuid NULL,
                initiator_id uuid NULL,
                response_address text NULL,
                fault_address text NULL,
                state smallint NOT NULL DEFAULT 0 CHECK (state BETWEEN 0 AND 4),
                next_attempt_at_utc timestamptz NOT NULL,
                scheduled_at_utc timestamptz NULL,
                lease_owner text NULL,
                lease_expires_at_utc timestamptz NULL,
                attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
                failure_category text NULL,
                dispatched_at_utc timestamptz NULL,
                cancelled_at_utc timestamptz NULL
            );

            ALTER TABLE myservicebus.outbox_message
                ADD COLUMN IF NOT EXISTS scheduled_at_utc timestamptz NULL,
                ADD COLUMN IF NOT EXISTS cancelled_at_utc timestamptz NULL;

            ALTER TABLE myservicebus.outbox_message
                DROP CONSTRAINT IF EXISTS outbox_message_state_check;

            ALTER TABLE myservicebus.outbox_message
                ADD CONSTRAINT outbox_message_state_check CHECK (state BETWEEN 0 AND 4);

            UPDATE myservicebus.schema_version SET version = 3 WHERE singleton AND version = 2;

            CREATE INDEX IF NOT EXISTS ix_outbox_message_dispatch
                ON myservicebus.outbox_message (service_name, next_attempt_at_utc, created_at_utc)
                WHERE state IN (0, 1);

            CREATE TABLE IF NOT EXISTS myservicebus.inbox_message (
                consumer_scope text NOT NULL,
                message_id uuid NOT NULL,
                state smallint NOT NULL DEFAULT 0 CHECK (state BETWEEN 0 AND 1),
                acquired_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
                completed_at_utc timestamptz NULL,
                PRIMARY KEY (consumer_scope, message_id)
            );

            CREATE INDEX IF NOT EXISTS ix_inbox_message_completed
                ON myservicebus.inbox_message (completed_at_utc)
                WHERE state = 1;
            """;
}
