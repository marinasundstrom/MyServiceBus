package com.myservicebus;

import java.lang.reflect.Proxy;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashSet;
import java.util.List;

public final class MessageUrn {
    private MessageUrn() { }

    public static String forClass(Class<?> messageType) {
        if (Proxy.isProxyClass(messageType) && messageType.getInterfaces().length > 0) {
            messageType = messageType.getInterfaces()[0];
        }
        MessageUrnName override = messageType.getAnnotation(MessageUrnName.class);
        if (override != null) {
            if (override.value().isBlank()) throw new IllegalArgumentException("Message URN must not be blank");
            return (override.useDefaultPrefix() ? "urn:message:" : "") + override.value();
        }
        return "urn:message:" + messageType.getPackageName() + ":" + messageType.getSimpleName();
    }

    public static String forFault(Class<?> messageType) {
        String urn = forClass(messageType);
        return "urn:message:MassTransit:Fault[[" + (urn.startsWith("urn:message:") ? urn.substring(12) : urn) + "]]";
    }

    public static List<String> forMessageTypes(Class<?> messageType) {
        return messageTypes(messageType).stream().map(MessageUrn::forClass).toList();
    }

    public static List<Class<?>> messageTypes(Class<?> messageType) {
        if (Proxy.isProxyClass(messageType) && messageType.getInterfaces().length > 0) {
            messageType = messageType.getInterfaces()[0];
        }

        LinkedHashSet<Class<?>> types = new LinkedHashSet<>();
        types.add(messageType);
        for (Class<?> baseType = messageType.getSuperclass(); baseType != null && baseType != Object.class;
                baseType = baseType.getSuperclass()) {
            if (isContractType(baseType)) types.add(baseType);
        }
        LinkedHashSet<Class<?>> discoveredInterfaces = new LinkedHashSet<>();
        for (Class<?> type = messageType; type != null && type != Object.class; type = type.getSuperclass()) {
            collectInterfaces(type, discoveredInterfaces);
        }
        List<Class<?>> interfaces = new ArrayList<>(discoveredInterfaces);
        interfaces.sort(Comparator.comparing(Class::getName));
        interfaces.stream().filter(MessageUrn::isContractType).forEach(types::add);
        return List.copyOf(types);
    }

    public static boolean isContractType(Class<?> type) {
        String name = type.getPackageName();
        return !name.startsWith("java.") && !name.startsWith("javax.") && !name.startsWith("jdk.");
    }

    private static void collectInterfaces(Class<?> type, LinkedHashSet<Class<?>> interfaces) {
        for (Class<?> interfaceType : type.getInterfaces()) {
            if (interfaces.add(interfaceType)) {
                collectInterfaces(interfaceType, interfaces);
            }
        }
    }
}
