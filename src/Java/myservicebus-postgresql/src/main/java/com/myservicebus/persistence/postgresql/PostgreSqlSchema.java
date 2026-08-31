package com.myservicebus.persistence.postgresql;

import java.sql.Connection;
import java.sql.SQLException;
import java.sql.Statement;
import java.util.Objects;
import javax.sql.DataSource;

public final class PostgreSqlSchema {
    public static final int CURRENT_VERSION = 4;

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
            VALUES (true, 4)
            ON CONFLICT (singleton) DO NOTHING;

            DO $migration$
            BEGIN
                IF (SELECT version FROM myservicebus.schema_version WHERE singleton) NOT IN (2, 3, 4) THEN
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

            CREATE TABLE IF NOT EXISTS myservicebus.recurring_job_definition (
                definition_id uuid PRIMARY KEY,
                service_name text NOT NULL CHECK (length(service_name) > 0),
                schedule_group text NOT NULL DEFAULT '',
                schedule_id text NOT NULL CHECK (length(schedule_id) > 0),
                revision bigint NOT NULL CHECK (revision > 0),
                semantic_hash text NOT NULL CHECK (length(semantic_hash) > 0),
                status smallint NOT NULL CHECK (status BETWEEN 0 AND 3),
                cadence_kind smallint NOT NULL CHECK (cadence_kind BETWEEN 0 AND 1),
                cadence jsonb NOT NULL,
                description text NULL,
                start_at_utc timestamptz NULL,
                end_at_utc timestamptz NULL,
                misfire_policy smallint NOT NULL CHECK (misfire_policy BETWEEN 0 AND 2),
                max_catch_up_occurrences integer NOT NULL CHECK (max_catch_up_occurrences > 0),
                overlap_policy smallint NOT NULL CHECK (overlap_policy BETWEEN 0 AND 2),
                delivery_intent smallint NOT NULL CHECK (delivery_intent BETWEEN 0 AND 1),
                destination_address text NOT NULL,
                job_type_name text NOT NULL CHECK (length(job_type_name) > 0),
                job_retry_limit integer NOT NULL CHECK (job_retry_limit >= 0),
                job_retry_delay_milliseconds bigint NULL CHECK (job_retry_delay_milliseconds >= 0),
                job_timeout_milliseconds bigint NOT NULL CHECK (job_timeout_milliseconds > 0),
                job_concurrent_limit integer NOT NULL CHECK (job_concurrent_limit > 0),
                command_message_types text[] NOT NULL CHECK (cardinality(command_message_types) > 0),
                command_payload jsonb NOT NULL,
                command_headers jsonb NOT NULL DEFAULT '{}'::jsonb,
                content_type text NOT NULL,
                accepted_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                next_due_at_utc timestamptz NULL,
                lease_owner text NULL,
                lease_expires_at_utc timestamptz NULL,
                UNIQUE (service_name, schedule_group, schedule_id),
                CHECK (end_at_utc IS NULL OR start_at_utc IS NULL OR end_at_utc > start_at_utc)
            );

            CREATE INDEX IF NOT EXISTS ix_recurring_job_definition_due
                ON myservicebus.recurring_job_definition (next_due_at_utc, definition_id)
                WHERE status = 0 AND next_due_at_utc IS NOT NULL;

            CREATE TABLE IF NOT EXISTS myservicebus.recurring_job_occurrence (
                occurrence_id uuid PRIMARY KEY,
                definition_id uuid NOT NULL REFERENCES myservicebus.recurring_job_definition (definition_id),
                definition_revision bigint NOT NULL CHECK (definition_revision > 0),
                scheduled_for_utc timestamptz NOT NULL,
                materialized_at_utc timestamptz NOT NULL,
                materialization_reason smallint NOT NULL CHECK (materialization_reason BETWEEN 0 AND 3),
                is_manual boolean NOT NULL DEFAULT false,
                status smallint NOT NULL CHECK (status BETWEEN 0 AND 8),
                job_id uuid NULL,
                failure_category text NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_recurring_job_occurrence_scheduled
                ON myservicebus.recurring_job_occurrence (
                    definition_id, definition_revision, scheduled_for_utc)
                WHERE NOT is_manual;

            CREATE INDEX IF NOT EXISTS ix_recurring_job_occurrence_history
                ON myservicebus.recurring_job_occurrence (definition_id, scheduled_for_utc DESC);

            UPDATE myservicebus.schema_version SET version = 4 WHERE singleton AND version = 3;

            CREATE TABLE IF NOT EXISTS myservicebus.job (
                job_id uuid PRIMARY KEY,
                service_name text NOT NULL CHECK (length(service_name) > 0),
                job_type_name text NOT NULL CHECK (length(job_type_name) > 0),
                message_types text[] NOT NULL CHECK (cardinality(message_types) > 0),
                body bytea NOT NULL,
                content_type text NOT NULL CHECK (length(content_type) > 0),
                headers jsonb NOT NULL DEFAULT '{}'::jsonb,
                status smallint NOT NULL CHECK (status BETWEEN 0 AND 6),
                submitted_at_utc timestamptz NOT NULL,
                scheduled_for_utc timestamptz NULL,
                available_at_utc timestamptz NOT NULL,
                started_at_utc timestamptz NULL,
                completed_at_utc timestamptz NULL,
                updated_at_utc timestamptz NOT NULL,
                retry_limit integer NOT NULL CHECK (retry_limit >= 0),
                retry_delay_milliseconds bigint NULL CHECK (retry_delay_milliseconds >= 0),
                timeout_milliseconds bigint NOT NULL CHECK (timeout_milliseconds > 0),
                concurrent_job_limit integer NOT NULL CHECK (concurrent_job_limit > 0),
                attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
                progress_value bigint NULL CHECK (progress_value >= 0),
                progress_limit bigint NULL CHECK (progress_limit > 0),
                recurring_occurrence_id uuid NULL,
                cancellation_requested_at_utc timestamptz NULL,
                lease_owner text NULL,
                lease_expires_at_utc timestamptz NULL,
                failure_type text NULL,
                failure_message text NULL,
                CHECK (progress_limit IS NULL OR progress_value IS NULL OR progress_value <= progress_limit)
            );

            CREATE INDEX IF NOT EXISTS ix_job_available
                ON myservicebus.job (service_name, available_at_utc, submitted_at_utc, job_id)
                WHERE status IN (1, 2);

            CREATE INDEX IF NOT EXISTS ix_job_expired_lease
                ON myservicebus.job (service_name, lease_expires_at_utc, job_id)
                WHERE status = 3;

            CREATE INDEX IF NOT EXISTS ix_job_history
                ON myservicebus.job (service_name, updated_at_utc DESC, job_id);

            CREATE INDEX IF NOT EXISTS ix_job_recurring_occurrence
                ON myservicebus.job (recurring_occurrence_id)
                WHERE recurring_occurrence_id IS NOT NULL;

            DO $constraint$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'recurring_job_occurrence_job_id_fkey'
                      AND conrelid = 'myservicebus.recurring_job_occurrence'::regclass) THEN
                    ALTER TABLE myservicebus.recurring_job_occurrence
                        ADD CONSTRAINT recurring_job_occurrence_job_id_fkey
                        FOREIGN KEY (job_id) REFERENCES myservicebus.job (job_id);
                END IF;
            END
            $constraint$;

            CREATE TABLE IF NOT EXISTS myservicebus.job_attempt (
                attempt_id uuid PRIMARY KEY,
                job_id uuid NOT NULL REFERENCES myservicebus.job (job_id) ON DELETE CASCADE,
                retry_attempt integer NOT NULL CHECK (retry_attempt >= 0),
                status smallint NOT NULL CHECK (status BETWEEN 0 AND 3),
                worker_id text NOT NULL CHECK (length(worker_id) > 0),
                started_at_utc timestamptz NOT NULL,
                completed_at_utc timestamptz NULL,
                fault_type text NULL,
                fault_message text NULL,
                UNIQUE (job_id, retry_attempt)
            );

            CREATE INDEX IF NOT EXISTS ix_job_attempt_history
                ON myservicebus.job_attempt (job_id, retry_attempt DESC);

            """;
}
