package com.myservicebus.choreography;

import com.myservicebus.MessageUrn;

import java.time.Duration;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Objects;
import java.util.function.Consumer;

/** Builds a normalized declaration of application-owned choreography reactions. */
public final class ChoreographyBuilder {
    private final String choreographyId;
    private final String definitionVersion;
    private final String owner;
    private final List<ChoreographyStep> steps = new ArrayList<>();

    public ChoreographyBuilder(String choreographyId, String definitionVersion, String owner) {
        this.choreographyId = required(choreographyId, "choreographyId");
        this.definitionVersion = required(definitionVersion, "definitionVersion");
        this.owner = required(owner, "owner");
    }

    public ChoreographyBuilder step(
            String id,
            Class<?> triggerMessageType,
            Consumer<ChoreographyStepBuilder> configure) {
        Objects.requireNonNull(triggerMessageType, "triggerMessageType");
        return step(id, MessageUrn.forClass(triggerMessageType), configure);
    }

    public ChoreographyBuilder step(
            String id,
            String triggerMessageUrn,
            Consumer<ChoreographyStepBuilder> configure) {
        Objects.requireNonNull(configure, "configure");

        String stepId = required(id, "id");
        if (steps.stream().anyMatch(step -> step.id().equals(stepId))) {
            throw new IllegalArgumentException("A choreography step with ID '" + stepId + "' is already declared.");
        }

        ChoreographyStepBuilder builder = new ChoreographyStepBuilder(
                stepId,
                required(triggerMessageUrn, "triggerMessageUrn"));
        configure.accept(builder);
        steps.add(builder.build());
        return this;
    }

    public ChoreographyFragment build() {
        if (steps.isEmpty()) {
            throw new IllegalStateException("A choreography fragment must declare at least one step.");
        }

        List<ChoreographyStep> normalizedSteps = steps.stream()
                .sorted(Comparator.comparing(ChoreographyStep::id))
                .toList();
        return new ChoreographyFragment(
                ChoreographyFragment.CURRENT_SCHEMA_VERSION,
                choreographyId,
                definitionVersion,
                owner,
                normalizedSteps);
    }

    static String required(String value, String parameterName) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(parameterName + " cannot be empty or whitespace.");
        }
        return value;
    }

    public static final class ChoreographyStepBuilder {
        private static final Comparator<String> NULLABLE_STRING_COMPARATOR =
                Comparator.nullsFirst(Comparator.naturalOrder());

        private final String id;
        private final String triggerMessageUrn;
        private final List<ChoreographyOutput> outputs = new ArrayList<>();
        private String ownerComponent;

        private ChoreographyStepBuilder(String id, String triggerMessageUrn) {
            this.id = id;
            this.triggerMessageUrn = triggerMessageUrn;
        }

        public ChoreographyStepBuilder ownedBy(Class<?> componentType) {
            Objects.requireNonNull(componentType, "componentType");
            return ownedBy(componentType.getName());
        }

        public ChoreographyStepBuilder ownedBy(String component) {
            ownerComponent = required(component, "component");
            return this;
        }

        public ChoreographyStepBuilder sends(Class<?> messageType, String destination) {
            return sends(messageType, destination, null);
        }

        public ChoreographyStepBuilder sends(
                Class<?> messageType,
                String destination,
                Consumer<ChoreographyOutputBuilder> configure) {
            Objects.requireNonNull(messageType, "messageType");
            return sends(MessageUrn.forClass(messageType), destination, configure);
        }

        public ChoreographyStepBuilder sends(String messageUrn, String destination) {
            return sends(messageUrn, destination, null);
        }

        public ChoreographyStepBuilder sends(
                String messageUrn,
                String destination,
                Consumer<ChoreographyOutputBuilder> configure) {
            return add(
                    ChoreographyOperationKind.SEND,
                    messageUrn,
                    required(destination, "destination"),
                    configure);
        }

        public ChoreographyStepBuilder publishes(Class<?> messageType) {
            return publishes(messageType, null);
        }

        public ChoreographyStepBuilder publishes(
                Class<?> messageType,
                Consumer<ChoreographyOutputBuilder> configure) {
            Objects.requireNonNull(messageType, "messageType");
            return publishes(MessageUrn.forClass(messageType), configure);
        }

        public ChoreographyStepBuilder publishes(String messageUrn) {
            return publishes(messageUrn, null);
        }

        public ChoreographyStepBuilder publishes(
                String messageUrn,
                Consumer<ChoreographyOutputBuilder> configure) {
            return add(ChoreographyOperationKind.PUBLISH, messageUrn, null, configure);
        }

        public ChoreographyStepBuilder responds(Class<?> messageType) {
            return responds(messageType, null);
        }

        public ChoreographyStepBuilder responds(
                Class<?> messageType,
                Consumer<ChoreographyOutputBuilder> configure) {
            Objects.requireNonNull(messageType, "messageType");
            return responds(MessageUrn.forClass(messageType), configure);
        }

        public ChoreographyStepBuilder responds(String messageUrn) {
            return responds(messageUrn, null);
        }

        public ChoreographyStepBuilder responds(
                String messageUrn,
                Consumer<ChoreographyOutputBuilder> configure) {
            return add(ChoreographyOperationKind.RESPOND, messageUrn, null, configure);
        }

        public ChoreographyStepBuilder schedules(Class<?> messageType) {
            return schedules(messageType, null, null);
        }

        public ChoreographyStepBuilder schedules(
                Class<?> messageType,
                Consumer<ChoreographyOutputBuilder> configure) {
            return schedules(messageType, null, configure);
        }

        public ChoreographyStepBuilder schedules(
                Class<?> messageType,
                String destination,
                Consumer<ChoreographyOutputBuilder> configure) {
            Objects.requireNonNull(messageType, "messageType");
            return schedules(MessageUrn.forClass(messageType), destination, configure);
        }

        public ChoreographyStepBuilder schedules(String messageUrn) {
            return schedules(messageUrn, null, null);
        }

        public ChoreographyStepBuilder schedules(
                String messageUrn,
                Consumer<ChoreographyOutputBuilder> configure) {
            return schedules(messageUrn, null, configure);
        }

        public ChoreographyStepBuilder schedules(
                String messageUrn,
                String destination,
                Consumer<ChoreographyOutputBuilder> configure) {
            return add(ChoreographyOperationKind.SCHEDULE, messageUrn, destination, configure);
        }

        public ChoreographyStepBuilder terminates() {
            return terminates(null);
        }

        public ChoreographyStepBuilder terminates(Consumer<ChoreographyOutputBuilder> configure) {
            return add(ChoreographyOperationKind.TERMINAL, null, null, configure);
        }

        private ChoreographyStep build() {
            if (outputs.isEmpty()) {
                throw new IllegalStateException(
                        "Choreography step '" + id + "' must declare at least one output or terminal outcome.");
            }

            List<ChoreographyOutput> normalizedOutputs = outputs.stream()
                    .sorted(Comparator.comparing(ChoreographyOutput::kind)
                            .thenComparing(ChoreographyOutput::messageUrn, NULLABLE_STRING_COMPARATOR)
                            .thenComparing(ChoreographyOutput::destination, NULLABLE_STRING_COMPARATOR)
                            .thenComparing(ChoreographyOutput::requirement)
                            .thenComparing(ChoreographyOutput::minCount, Comparator.nullsFirst(Comparator.naturalOrder()))
                            .thenComparing(ChoreographyOutput::maxCount, Comparator.nullsFirst(Comparator.naturalOrder()))
                            .thenComparing(
                                    ChoreographyOutput::withinMilliseconds,
                                    Comparator.nullsFirst(Comparator.naturalOrder())))
                    .toList();
            return new ChoreographyStep(id, triggerMessageUrn, ownerComponent, normalizedOutputs);
        }

        private ChoreographyStepBuilder add(
                ChoreographyOperationKind kind,
                String messageUrn,
                String destination,
                Consumer<ChoreographyOutputBuilder> configure) {
            if (kind != ChoreographyOperationKind.TERMINAL) {
                messageUrn = required(messageUrn, "messageUrn");
            }

            ChoreographyOutputBuilder builder = new ChoreographyOutputBuilder(kind, messageUrn, destination);
            if (configure != null) {
                configure.accept(builder);
            }
            outputs.add(builder.build());
            return this;
        }
    }

    public static final class ChoreographyOutputBuilder {
        private final ChoreographyOperationKind kind;
        private final String messageUrn;
        private final String destination;
        private ChoreographyRequirement requirement = ChoreographyRequirement.EXPECTED;
        private Integer minCount;
        private Integer maxCount;
        private Long withinMilliseconds;

        private ChoreographyOutputBuilder(
                ChoreographyOperationKind kind,
                String messageUrn,
                String destination) {
            this.kind = kind;
            this.messageUrn = messageUrn;
            this.destination = destination;
        }

        public ChoreographyOutputBuilder informational() {
            requirement = ChoreographyRequirement.INFORMATIONAL;
            return this;
        }

        public ChoreographyOutputBuilder optional() {
            requirement = ChoreographyRequirement.OPTIONAL;
            return this;
        }

        public ChoreographyOutputBuilder expected() {
            requirement = ChoreographyRequirement.EXPECTED;
            return this;
        }

        public ChoreographyOutputBuilder atLeast(int count) {
            minCount = nonNegative(count, "count");
            return this;
        }

        public ChoreographyOutputBuilder atMost(int count) {
            maxCount = nonNegative(count, "count");
            return this;
        }

        public ChoreographyOutputBuilder exactly(int count) {
            minCount = maxCount = nonNegative(count, "count");
            return this;
        }

        public ChoreographyOutputBuilder within(Duration duration) {
            Objects.requireNonNull(duration, "duration");
            long milliseconds = duration.toMillis();
            if (milliseconds <= 0) {
                throw new IllegalArgumentException("The time expectation must be at least one millisecond.");
            }
            withinMilliseconds = milliseconds;
            return this;
        }

        private ChoreographyOutput build() {
            if (minCount != null && maxCount != null && minCount > maxCount) {
                throw new IllegalStateException("The minimum output count cannot exceed the maximum output count.");
            }
            if (kind == ChoreographyOperationKind.TERMINAL
                    && (minCount != null || maxCount != null || withinMilliseconds != null)) {
                throw new IllegalStateException(
                        "A terminal outcome cannot declare output count or timing expectations.");
            }
            return new ChoreographyOutput(
                    kind,
                    messageUrn,
                    destination,
                    requirement,
                    minCount,
                    maxCount,
                    withinMilliseconds);
        }

        private static int nonNegative(int count, String parameterName) {
            if (count < 0) {
                throw new IllegalArgumentException(parameterName + " cannot be negative.");
            }
            return count;
        }
    }
}
