package com.myservicebus;

public interface JobProvider extends JobClient, JobSource {
    String getProviderName();

    SchedulingDurability getDurability();

    SchedulingPlacement getPlacement();

    @Override
    default String getProvider() {
        return getProviderName();
    }
}
