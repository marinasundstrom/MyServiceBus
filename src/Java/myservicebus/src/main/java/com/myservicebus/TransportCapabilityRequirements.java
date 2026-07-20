package com.myservicebus;

import java.util.ArrayList;
import java.util.List;

public final class TransportCapabilityRequirements {
    private final List<TransportCapabilityRequirement> items = new ArrayList<>();

    public void require(String capability, boolean requireNative) {
        items.add(new TransportCapabilityRequirement(capability, requireNative));
    }

    public List<TransportCapabilityRequirement> items() {
        return List.copyOf(items);
    }
}
