package com.myservicebus;

import java.net.URI;
import java.util.*;

/** Bus-local contract identities. Configuration is immutable after freeze. */
public final class MessageContractRegistry {
    private final Map<Class<?>, String> overrides = new HashMap<>();
    private final Map<String, Class<?>> owners = new HashMap<>();
    private boolean frozen;

    public synchronized void setMessageUrn(Class<?> type, String urn) {
        Objects.requireNonNull(type, "type");
        if (urn == null || urn.isBlank() || !URI.create(urn).isAbsolute())
            throw new IllegalArgumentException("A message identity must be an absolute URN or URI");
        if (frozen) throw new IllegalStateException("Message contracts are frozen");
        overrides.put(type, urn);
    }

    public synchronized String getMessageUrn(Class<?> type) {
        if (java.lang.reflect.Proxy.isProxyClass(type)) type = type.getInterfaces()[0];
        String urn = overrides.containsKey(type) ? overrides.get(type) : MessageUrn.forClass(type);
        if (!URI.create(urn).isAbsolute()) throw new IllegalArgumentException("Message identity must be an absolute URN or URI");
        Class<?> owner = owners.putIfAbsent(urn, type);
        if (owner != null && !owner.equals(type))
            throw new IllegalStateException("Message identity '" + urn + "' is shared by " + owner + " and " + type);
        return urn;
    }

    public String getFaultUrn(Class<?> type) {
        String urn = getMessageUrn(type);
        return "urn:message:MassTransit:Fault[[" + (urn.startsWith("urn:message:") ? urn.substring(12) : urn) + "]]";
    }

    public List<String> getMessageUrns(Class<?> type) {
        return MessageUrn.messageTypes(type).stream().map(this::getMessageUrn).toList();
    }

    public synchronized void freeze(Collection<? extends Class<?>> types) {
        if (frozen) return;
        owners.clear();
        Set<Class<?>> all = new HashSet<>(types);
        all.addAll(overrides.keySet());
        all.forEach(this::getMessageUrns);
        frozen = true;
    }
}
