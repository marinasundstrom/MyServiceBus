package com.myservicebus.di;

import com.google.inject.Key;
import com.google.inject.Provider;
import com.google.inject.Scope;

import java.util.HashMap;
import java.util.Map;

public class PerMessageScope implements Scope {
    private final ThreadLocal<Map<Key<?>, Object>> scopeContext = new ThreadLocal<>();

    public void enter() {
        if (scopeContext.get() != null) {
            throw new IllegalStateException("Scope already entered");
        }
        scopeContext.set(new HashMap<>());
    }

    public void exit() {
        if (scopeContext.get() == null) {
            throw new IllegalStateException("No scope to exit");
        }
        scopeContext.remove();
    }

    @Override
    public <T> Provider<T> scope(Key<T> key, Provider<T> unscoped) {
        return () -> {
            Map<Key<?>, Object> scope = scopeContext.get();
            if (scope == null) {
                throw new IllegalStateException("No scope active. Did you forget to call enter()?");
            }

            // manual computeIfAbsent
            @SuppressWarnings("unchecked")
            T instance = (T) scope.get(key);
            if (instance == null) {
                instance = unscoped.get(); // ← can trigger nested scoped resolutions
                scope.put(key, instance); // ← only after it's safely constructed
            }

            return instance;
        };
    }
}