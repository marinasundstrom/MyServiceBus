package com.myservicebus;

public record CronRecurringJobCadence(
        String expression,
        RecurringJobCronDialect dialect,
        String timeZoneId) implements RecurringJobCadence {
    /**
     * Creates a cron cadence whose expression is interpreted only using the declared dialect.
     *
     * @throws IllegalArgumentException when an argument is blank or the dialect is null
     */
    public CronRecurringJobCadence {
        if (expression == null || expression.isBlank()) {
            throw new IllegalArgumentException("expression must not be blank");
        }
        if (dialect == null) {
            throw new IllegalArgumentException("dialect must not be null");
        }
        if (timeZoneId == null || timeZoneId.isBlank()) {
            throw new IllegalArgumentException("timeZoneId must not be blank");
        }
        expression = expression.trim();
        timeZoneId = timeZoneId.trim();
    }

    public CronRecurringJobCadence(String expression, RecurringJobCronDialect dialect) {
        this(expression, dialect, "UTC");
    }
}
