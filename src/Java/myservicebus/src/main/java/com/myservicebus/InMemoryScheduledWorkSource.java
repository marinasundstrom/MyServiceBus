package com.myservicebus;

import java.util.Comparator;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;

public final class InMemoryScheduledWorkSource implements ScheduledWorkSource {
    private final ConcurrentMap<UUID, ScheduledWorkState> items = new ConcurrentHashMap<>();

    @Override
    public String getProvider() {
        return "InMemory";
    }

    @Override
    public boolean isAuthoritative() {
        return true;
    }

    @Override
    public List<ScheduledWorkState> getSnapshot() {
        return items.values().stream()
                .sorted(Comparator.comparing(ScheduledWorkState::dueAtUtc))
                .toList();
    }

    void upsert(ScheduledWorkState state) {
        items.put(state.tokenId(), state);
    }

    ScheduledWorkState get(UUID tokenId) {
        return items.get(tokenId);
    }

    ScheduledWorkState remove(UUID tokenId) {
        return items.remove(tokenId);
    }
}
