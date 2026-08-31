package com.myservicebus;

public record RecurringJobIdentity(String scheduleId, String scheduleGroup) {
    /**
     * Creates the caller-owned identity of a recurring job definition.
     *
     * @throws IllegalArgumentException when {@code scheduleId} is blank
     */
    public RecurringJobIdentity {
        scheduleId = requireValue(scheduleId, "scheduleId");
        scheduleGroup = normalizeOptional(scheduleGroup);
    }

    public RecurringJobIdentity(String scheduleId) {
        this(scheduleId, null);
    }

    private static String requireValue(String value, String parameterName) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(parameterName + " must not be blank");
        }
        return value.trim();
    }

    private static String normalizeOptional(String value) {
        return value == null || value.isBlank() ? null : value.trim();
    }
}
