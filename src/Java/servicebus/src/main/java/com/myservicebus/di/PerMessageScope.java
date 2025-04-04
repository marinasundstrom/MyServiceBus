package com.myservicebus.di;

import com.google.inject.Key;
import com.google.inject.Provider;
import com.google.inject.Scope;

import java.util.HashMap;
import java.util.Map;

public class PerMessageScope implements Scope {
    private final ThreadLocal<Map<Key<?>, Object>> scopeContext = ThreadLocal.withInitial(HashMap::new);

    public void enter() {
        scopeContext.set(new HashMap<>());
    }

    public void exit() {
        scopeContext.remove();
    }

    @Override
    public <T> Provider<T> scope(Key<T> key, Provider<T> unscoped) {
        return () -> {
            Map<Key<?>, Object> scope = scopeContext.get();
            return (T) scope.computeIfAbsent(key, k -> unscoped.get());
        };
    }
}