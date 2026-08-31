package com.myservicebus;

/**
 * Provider integration boundary for recurring definitions and occurrence materialization.
 * Applications use {@link RecurringJobScheduler} instead.
 */
public interface RecurringJobProvider extends RecurringJobScheduler {
    String getProviderName();

    SchedulingDurability getDurability();

    SchedulingPlacement getPlacement();
}
