package com.myservicebus;

import java.util.Map;

public record FilterDescriptor(
        int order,
        String kind,
        String implementation,
        FilterLifetime lifetime,
        Map<String, String> configuration) {

    public FilterDescriptor {
        configuration = Map.copyOf(configuration);
    }
}
