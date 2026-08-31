package com.myservicebus;

public sealed interface RecurringJobCadence
        permits FixedIntervalRecurringJobCadence, CronRecurringJobCadence {
}
