package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.time.Duration;
import java.time.Instant;
import org.junit.jupiter.api.Test;

class RecurringJobContractTest {
    @Test
    void identityNormalizesCallerSuppliedValues() {
        RecurringJobIdentity identity = new RecurringJobIdentity(" daily-export ", " billing ");

        assertEquals("daily-export", identity.scheduleId());
        assertEquals("billing", identity.scheduleGroup());
        assertEquals(new RecurringJobIdentity("daily-export", "billing"), identity);
    }

    @Test
    void identityRejectsBlankScheduleId() {
        assertThrows(IllegalArgumentException.class, () -> new RecurringJobIdentity("  "));
    }

    @Test
    void fixedIntervalRequiresPositiveDurationAndKeepsInstantAnchor() {
        assertThrows(IllegalArgumentException.class,
                () -> new FixedIntervalRecurringJobCadence(Duration.ZERO));

        Instant anchor = Instant.parse("2026-09-01T01:00:00Z");
        FixedIntervalRecurringJobCadence cadence =
                new FixedIntervalRecurringJobCadence(Duration.ofMinutes(15), anchor);

        assertEquals(Duration.ofMinutes(15), cadence.interval());
        assertEquals(anchor, cadence.anchorAtUtc());
    }

    @Test
    void cronRequiresExplicitDialectAndDefaultsToUtc() {
        CronRecurringJobCadence cadence =
                new CronRecurringJobCadence(" 0 1 * * * ", RecurringJobCronDialect.UNIX5);

        assertEquals("0 1 * * *", cadence.expression());
        assertEquals(RecurringJobCronDialect.UNIX5, cadence.dialect());
        assertEquals("UTC", cadence.timeZoneId());
    }

    @Test
    void definitionHasSafeMvpDefaults() {
        RecurringJobDefinition definition = new RecurringJobDefinition(
                new RecurringJobIdentity("daily-export"),
                new FixedIntervalRecurringJobCadence(Duration.ofDays(1)));

        assertEquals(RecurringJobMisfirePolicy.FIRE_ONCE_NOW, definition.misfirePolicy());
        assertEquals(1, definition.maxCatchUpOccurrences());
        assertEquals(RecurringJobOverlapPolicy.ALLOW, definition.overlapPolicy());
        assertNull(definition.description());
    }

    @Test
    void definitionRejectsInvalidWindowOrCatchUpCap() {
        RecurringJobIdentity identity = new RecurringJobIdentity("daily-export");
        RecurringJobCadence cadence = new FixedIntervalRecurringJobCadence(Duration.ofDays(1));
        Instant start = Instant.parse("2026-09-02T00:00:00Z");

        assertThrows(IllegalArgumentException.class, () -> new RecurringJobDefinition(
                identity, cadence, null, start, start,
                RecurringJobMisfirePolicy.FIRE_ONCE_NOW, 1, RecurringJobOverlapPolicy.ALLOW));
        assertThrows(IllegalArgumentException.class, () -> new RecurringJobDefinition(
                identity, cadence, null, null, null,
                RecurringJobMisfirePolicy.CATCH_UP, 0, RecurringJobOverlapPolicy.ALLOW));
    }
}
