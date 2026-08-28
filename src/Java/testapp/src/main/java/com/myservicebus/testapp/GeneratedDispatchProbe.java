package com.myservicebus.testapp;

import com.myservicebus.ConsumeContext;
import com.myservicebus.tasks.CancellationToken;

public final class GeneratedDispatchProbe {
    private GeneratedDispatchMessage message;
    private ConsumeContext<GeneratedDispatchMessage> context;
    private CancellationToken cancellationToken;

    public void record(
            GeneratedDispatchMessage message,
            ConsumeContext<GeneratedDispatchMessage> context,
            CancellationToken cancellationToken) {
        this.message = message;
        this.context = context;
        this.cancellationToken = cancellationToken;
    }

    public GeneratedDispatchMessage getMessage() {
        return message;
    }

    public ConsumeContext<GeneratedDispatchMessage> getContext() {
        return context;
    }

    public CancellationToken getCancellationToken() {
        return cancellationToken;
    }
}
