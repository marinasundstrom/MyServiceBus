package com.myservicebus.persistence.postgresql;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import com.myservicebus.FixedIntervalRecurringJobCadence;
import com.myservicebus.MessageUrn;
import com.myservicebus.PublishContext;
import com.myservicebus.RecurringJobControlOutcome;
import com.myservicebus.RecurringJobControlResult;
import com.myservicebus.RecurringJobDefinition;
import com.myservicebus.RecurringJobDefinitionReceipt;
import com.myservicebus.RecurringJobDefinitionStatus;
import com.myservicebus.RecurringJobIdentity;
import com.myservicebus.RecurringJobOccurrenceReceipt;
import com.myservicebus.RecurringJobOverlapPolicy;
import com.myservicebus.RecurringJobProvider;
import com.myservicebus.RecurringJobRevisionConflictException;
import com.myservicebus.SchedulingDurability;
import com.myservicebus.SchedulingPlacement;
import com.myservicebus.TransportFactory;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.serialization.MessageSerializerMetadata;
import com.myservicebus.serialization.MessageEnvelopeMode;
import com.myservicebus.tasks.CancellationToken;
import java.math.BigInteger;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Types;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.HexFormat;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import javax.sql.DataSource;

final class PostgreSqlRecurringJobProvider implements RecurringJobProvider {
    private record CurrentDefinition(
            UUID definitionId,
            long revision,
            RecurringJobDefinitionStatus status,
            String semanticHash,
            Instant acceptedAtUtc,
            Instant nextDueAtUtc) {
    }

    private static final ObjectMapper MAPPER = new ObjectMapper().findAndRegisterModules();
    private final DataSource dataSource;
    private final String serviceName;
    private final TransportFactory transportFactory;
    private final MessageSerializer serializer;
    private final Clock clock;

    PostgreSqlRecurringJobProvider(
            DataSource dataSource,
            String serviceName,
            TransportFactory transportFactory,
            MessageSerializer serializer,
            Clock clock) {
        this.dataSource = Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        this.serviceName = serviceName.trim();
        this.transportFactory = Objects.requireNonNull(transportFactory, "transportFactory");
        this.serializer = Objects.requireNonNull(serializer, "serializer");
        this.clock = clock == null ? Clock.systemUTC() : clock;
    }

    @Override
    public String getProviderName() {
        return "MyServiceBus.Durable";
    }

    @Override
    public SchedulingDurability getDurability() {
        return SchedulingDurability.DURABLE;
    }

    @Override
    public SchedulingPlacement getPlacement() {
        return SchedulingPlacement.EMBEDDED;
    }

    @Override
    public <TJob> CompletionStage<RecurringJobDefinitionReceipt> addOrUpdate(
            RecurringJobDefinition definition,
            TJob job,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        try {
            Objects.requireNonNull(definition, "definition");
            Objects.requireNonNull(job, "job");
            cancellationToken.throwIfCancelled();
            ensureSupported(definition);
            if (!(serializer instanceof MessageSerializerMetadata metadata)
                    || metadata.getEnvelopeMode() != MessageEnvelopeMode.ENVELOPE) {
                throw new UnsupportedOperationException(
                        "The interoperable durable provider requires the MyServiceBus envelope format.");
            }
            Instant now = clock.instant();
            String cadenceJson = createCadenceJson((FixedIntervalRecurringJobCadence) definition.cadence());
            List<String> messageTypes = MessageUrn.forMessageTypes(job.getClass());
            String destination = transportFactory.getPublishAddress(job.getClass());
            PublishContext context = new PublishContext(job, cancellationToken);
            context.setDestinationAddress(URI.create(destination));
            context.setMessageTypes(messageTypes);
            String commandEnvelope = new String(context.serialize(serializer), StandardCharsets.UTF_8);
            JsonNode envelope = MAPPER.readTree(commandEnvelope);
            commandEnvelope = MAPPER.writeValueAsString(envelope);
            String semanticHash = createSemanticHash(
                    definition,
                    cadenceJson,
                    destination,
                    messageTypes,
                    MAPPER.writeValueAsString(envelope.get("message")));

            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                try {
                    CurrentDefinition current = readCurrent(connection, definition.identity());
                    validateExpectedRevision(
                            definition.identity(), expectedRevision, current == null ? 0 : current.revision());
                    if (current != null
                            && current.status() != RecurringJobDefinitionStatus.REMOVED
                            && current.semanticHash().equals(semanticHash)) {
                        connection.commit();
                        return CompletableFuture.completedFuture(createReceipt(definition.identity(), current));
                    }

                    UUID definitionId = current == null ? UUID.randomUUID() : current.definitionId();
                    long revision = (current == null ? 0 : current.revision()) + 1;
                    Instant next = calculateNext(definition, now, now);
                    writeDefinition(
                            connection,
                            definitionId,
                            revision,
                            semanticHash,
                            definition,
                            cadenceJson,
                            destination,
                            messageTypes,
                            commandEnvelope,
                            now,
                            next,
                            current != null);
                    connection.commit();
                    return CompletableFuture.completedFuture(new RecurringJobDefinitionReceipt(
                            definitionId,
                            definition.identity(),
                            revision,
                            getProviderName(),
                            getDurability(),
                            getPlacement(),
                            now,
                            next));
                } catch (Exception failure) {
                    connection.rollback();
                    throw failure;
                }
            }
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    @Override
    public CompletionStage<RecurringJobControlResult> pause(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        return changeStatus(identity, expectedRevision, RecurringJobDefinitionStatus.PAUSED, cancellationToken);
    }

    @Override
    public CompletionStage<RecurringJobControlResult> resume(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        return changeStatus(identity, expectedRevision, RecurringJobDefinitionStatus.ACTIVE, cancellationToken);
    }

    @Override
    public CompletionStage<RecurringJobControlResult> remove(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        return changeStatus(identity, expectedRevision, RecurringJobDefinitionStatus.REMOVED, cancellationToken);
    }

    @Override
    public CompletionStage<RecurringJobOccurrenceReceipt> triggerNow(
            RecurringJobIdentity identity,
            CancellationToken cancellationToken) {
        return CompletableFuture.failedFuture(new UnsupportedOperationException(
                "Manual durable occurrence materialization is added with the outbox materializer slice."));
    }

    private CompletionStage<RecurringJobControlResult> changeStatus(
            RecurringJobIdentity identity,
            Long expectedRevision,
            RecurringJobDefinitionStatus requestedStatus,
            CancellationToken cancellationToken) {
        try {
            Objects.requireNonNull(identity, "identity");
            cancellationToken.throwIfCancelled();
            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                try {
                    CurrentDefinition current = readCurrent(connection, identity);
                    if (current == null || current.status() == RecurringJobDefinitionStatus.REMOVED) {
                        connection.rollback();
                        return CompletableFuture.completedFuture(
                                new RecurringJobControlResult(RecurringJobControlOutcome.NOT_FOUND));
                    }
                    validateExpectedRevision(identity, expectedRevision, current.revision());
                    if (current.status() == requestedStatus) {
                        connection.rollback();
                        return CompletableFuture.completedFuture(new RecurringJobControlResult(
                                RecurringJobControlOutcome.UNCHANGED, current.revision()));
                    }
                    Instant nextDue = switch (requestedStatus) {
                        case ACTIVE -> current.nextDueAtUtc() == null ? clock.instant() : current.nextDueAtUtc();
                        case PAUSED -> current.nextDueAtUtc();
                        default -> null;
                    };
                    try (PreparedStatement statement = connection.prepareStatement("""
                            UPDATE myservicebus.recurring_job_definition
                            SET status = ?, revision = revision + 1, updated_at_utc = ?, next_due_at_utc = ?,
                                lease_owner = NULL, lease_expires_at_utc = NULL
                            WHERE definition_id = ?
                            """)) {
                        statement.setShort(1, (short) requestedStatus.ordinal());
                        setInstant(statement, 2, clock.instant());
                        setInstant(statement, 3, nextDue);
                        statement.setObject(4, current.definitionId());
                        statement.executeUpdate();
                    }
                    connection.commit();
                    return CompletableFuture.completedFuture(new RecurringJobControlResult(
                            RecurringJobControlOutcome.APPLIED, current.revision() + 1));
                } catch (Exception failure) {
                    connection.rollback();
                    throw failure;
                }
            }
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    private CurrentDefinition readCurrent(Connection connection, RecurringJobIdentity identity) throws SQLException {
        try (PreparedStatement statement = connection.prepareStatement("""
                SELECT definition_id, revision, status, semantic_hash, accepted_at_utc, next_due_at_utc
                FROM myservicebus.recurring_job_definition
                WHERE service_name = ? AND schedule_group = ? AND schedule_id = ?
                FOR UPDATE
                """)) {
            statement.setString(1, serviceName);
            statement.setString(2, identity.scheduleGroup() == null ? "" : identity.scheduleGroup());
            statement.setString(3, identity.scheduleId());
            try (ResultSet result = statement.executeQuery()) {
                if (!result.next()) {
                    return null;
                }
                return new CurrentDefinition(
                        result.getObject(1, UUID.class),
                        result.getLong(2),
                        RecurringJobDefinitionStatus.values()[result.getShort(3)],
                        result.getString(4),
                        result.getObject(5, OffsetDateTime.class).toInstant(),
                        toInstant(result.getObject(6, OffsetDateTime.class)));
            }
        }
    }

    private void writeDefinition(
            Connection connection,
            UUID definitionId,
            long revision,
            String semanticHash,
            RecurringJobDefinition definition,
            String cadenceJson,
            String destination,
            List<String> messageTypes,
            String commandEnvelope,
            Instant now,
            Instant next,
            boolean update) throws SQLException {
        String sql = update ? """
                UPDATE myservicebus.recurring_job_definition SET
                    revision = ?, semantic_hash = ?, status = 0, cadence_kind = 0, cadence = ?::jsonb,
                    description = ?, start_at_utc = ?, end_at_utc = ?, misfire_policy = ?,
                    max_catch_up_occurrences = ?, overlap_policy = ?, delivery_intent = 1,
                    destination_address = ?, command_message_types = ?, command_payload = ?::jsonb,
                    command_headers = '{}'::jsonb, content_type = ?, accepted_at_utc = ?, updated_at_utc = ?,
                    next_due_at_utc = ?, lease_owner = NULL, lease_expires_at_utc = NULL
                WHERE definition_id = ?
                """ : """
                INSERT INTO myservicebus.recurring_job_definition (
                    revision, semantic_hash, status, cadence_kind, cadence, description, start_at_utc, end_at_utc,
                    misfire_policy, max_catch_up_occurrences, overlap_policy, delivery_intent,
                    destination_address, command_message_types, command_payload, command_headers, content_type,
                    accepted_at_utc, updated_at_utc, next_due_at_utc, definition_id,
                    service_name, schedule_group, schedule_id)
                VALUES (?, ?, 0, 0, ?::jsonb, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?::jsonb, '{}'::jsonb, ?, ?, ?, ?, ?, ?, ?, ?)
                """;
        try (PreparedStatement statement = connection.prepareStatement(sql)) {
            int index = 1;
            statement.setLong(index++, revision);
            statement.setString(index++, semanticHash);
            statement.setString(index++, cadenceJson);
            setNullable(statement, index++, definition.description(), Types.VARCHAR);
            setInstant(statement, index++, definition.startAtUtc());
            setInstant(statement, index++, definition.endAtUtc());
            statement.setShort(index++, (short) definition.misfirePolicy().ordinal());
            statement.setInt(index++, definition.maxCatchUpOccurrences());
            statement.setShort(index++, (short) definition.overlapPolicy().ordinal());
            statement.setString(index++, destination);
            statement.setArray(index++, connection.createArrayOf("text", messageTypes.toArray()));
            statement.setString(index++, commandEnvelope);
            statement.setString(index++, serializer.getContentType());
            setInstant(statement, index++, now);
            setInstant(statement, index++, now);
            setInstant(statement, index++, next);
            statement.setObject(index++, definitionId);
            if (!update) {
                statement.setString(index++, serviceName);
                statement.setString(index++, definition.identity().scheduleGroup() == null
                        ? "" : definition.identity().scheduleGroup());
                statement.setString(index, definition.identity().scheduleId());
            }
            statement.executeUpdate();
        }
    }

    private static String createCadenceJson(FixedIntervalRecurringJobCadence cadence) throws Exception {
        ObjectNode node = MAPPER.createObjectNode();
        node.put("kind", "fixedInterval");
        BigInteger nanoseconds = BigInteger.valueOf(cadence.interval().getSeconds())
                .multiply(BigInteger.valueOf(1_000_000_000L))
                .add(BigInteger.valueOf(cadence.interval().getNano()));
        node.put("intervalNanoseconds", nanoseconds.toString());
        if (cadence.anchorAtUtc() == null) {
            node.putNull("anchorAtUtc");
        } else {
            node.put("anchorAtUtc", cadence.anchorAtUtc().toString());
        }
        return MAPPER.writeValueAsString(node);
    }

    private static String createSemanticHash(
            RecurringJobDefinition definition,
            String cadence,
            String destination,
            List<String> messageTypes,
            String commandMessage) throws Exception {
        String value = String.join("\n",
                cadence,
                Objects.toString(definition.description(), ""),
                Objects.toString(definition.startAtUtc(), ""),
                Objects.toString(definition.endAtUtc(), ""),
                Integer.toString(definition.misfirePolicy().ordinal()),
                Integer.toString(definition.maxCatchUpOccurrences()),
                Integer.toString(definition.overlapPolicy().ordinal()),
                destination,
                String.join("\u001f", messageTypes),
                commandMessage);
        return HexFormat.of().withUpperCase().formatHex(
                MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8)));
    }

    private static Instant calculateNext(
            RecurringJobDefinition definition,
            Instant afterUtc,
            Instant acceptedAtUtc) {
        FixedIntervalRecurringJobCadence cadence = (FixedIntervalRecurringJobCadence) definition.cadence();
        Instant anchor = cadence.anchorAtUtc() != null
                ? cadence.anchorAtUtc()
                : definition.startAtUtc() != null ? definition.startAtUtc() : acceptedAtUtc;
        Instant threshold = definition.startAtUtc() != null && definition.startAtUtc().isAfter(afterUtc)
                ? definition.startAtUtc().minusNanos(1)
                : afterUtc;
        Instant next;
        if (anchor.isAfter(threshold)) {
            next = anchor;
        } else {
            long steps = Duration.between(anchor, threshold).dividedBy(cadence.interval()) + 1;
            next = anchor.plus(cadence.interval().multipliedBy(steps));
        }
        return definition.endAtUtc() != null && !next.isBefore(definition.endAtUtc()) ? null : next;
    }

    private RecurringJobDefinitionReceipt createReceipt(
            RecurringJobIdentity identity,
            CurrentDefinition current) {
        return new RecurringJobDefinitionReceipt(
                current.definitionId(), identity, current.revision(), getProviderName(), getDurability(),
                getPlacement(), current.acceptedAtUtc(), current.nextDueAtUtc());
    }

    private static void ensureSupported(RecurringJobDefinition definition) {
        if (!(definition.cadence() instanceof FixedIntervalRecurringJobCadence)) {
            throw new UnsupportedOperationException(
                    "The built-in durable provider currently supports fixed intervals only.");
        }
        if (definition.overlapPolicy() != RecurringJobOverlapPolicy.ALLOW) {
            throw new UnsupportedOperationException(
                    "The dispatch-only durable provider supports the ALLOW overlap policy only.");
        }
    }

    private static void validateExpectedRevision(
            RecurringJobIdentity identity,
            Long expectedRevision,
            long currentRevision) {
        if (expectedRevision != null && expectedRevision != currentRevision) {
            throw new RecurringJobRevisionConflictException(identity, expectedRevision, currentRevision);
        }
    }

    private static void setNullable(PreparedStatement statement, int index, Object value, int sqlType)
            throws SQLException {
        if (value == null) {
            statement.setNull(index, sqlType);
        } else {
            statement.setObject(index, value, sqlType);
        }
    }

    private static void setInstant(PreparedStatement statement, int index, Instant value) throws SQLException {
        if (value == null) {
            statement.setNull(index, Types.TIMESTAMP_WITH_TIMEZONE);
        } else {
            statement.setObject(index, value.atOffset(ZoneOffset.UTC));
        }
    }

    private static Instant toInstant(OffsetDateTime value) {
        return value == null ? null : value.toInstant();
    }
}
