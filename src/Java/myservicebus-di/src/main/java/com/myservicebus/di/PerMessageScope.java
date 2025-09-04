package com.myservicebus.di;

import com.google.inject.Key;
import com.google.inject.Provider;
import com.google.inject.Scope;

import java.util.ArrayDeque;
import java.util.Deque;
import java.util.HashMap;
import java.util.Map;

public class PerMessageScope implements Scope {
    private final ThreadLocal<Deque<Map<Key<?>, Object>>> scopeContext = new ThreadLocal<>();

    public void enter() {
        Deque<Map<Key<?>, Object>> deque = scopeContext.get();
        if (deque == null) {
            deque = new ArrayDeque<>();
            scopeContext.set(deque);
        }
        deque.push(new HashMap<>());
    }

    public void exit() {
        Deque<Map<Key<?>, Object>> deque = scopeContext.get();
        if (deque == null || deque.isEmpty()) {
            throw new IllegalStateException("No scope to exit");
        }
        deque.pop();
        if (deque.isEmpty()) {
            scopeContext.remove();
        }
    }

    @Override
    public <T> Provider<T> scope(Key<T> key, Provider<T> unscoped) {
        return () -> {
            Deque<Map<Key<?>, Object>> deque = scopeContext.get();
            Map<Key<?>, Object> scope = deque != null ? deque.peek() : null;
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