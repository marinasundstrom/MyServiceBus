package com.myservicebus;

import java.util.List;

public record PipelineDescriptor(
        int version,
        List<FilterDescriptor> filters) {

    public static final int CURRENT_VERSION = 1;

    public PipelineDescriptor(List<FilterDescriptor> filters) {
        this(CURRENT_VERSION, filters);
    }

    public PipelineDescriptor {
        filters = List.copyOf(filters);
    }
}
