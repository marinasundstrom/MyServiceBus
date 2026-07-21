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
        return String.format("urn:message:TestApp:%s", messageType.getSimpleName());
    }

    public static String forFault(Class<?> messageType) {
        return String.format("urn:message:MassTransit:Fault[[TestApp:%s]]", messageType.getSimpleName());
    }

    public static List<String> forMessageTypes(Class<?> messageType) {
        if (Proxy.isProxyClass(messageType) && messageType.getInterfaces().length > 0) {
            messageType = messageType.getInterfaces()[0];
        }

        LinkedHashSet<Class<?>> types = new LinkedHashSet<>();
        types.add(messageType);
        for (Class<?> baseType = messageType.getSuperclass(); baseType != null && baseType != Object.class;
                baseType = baseType.getSuperclass()) {
            types.add(baseType);
        }
        LinkedHashSet<Class<?>> discoveredInterfaces = new LinkedHashSet<>();
        for (Class<?> type = messageType; type != null && type != Object.class; type = type.getSuperclass()) {
            collectInterfaces(type, discoveredInterfaces);
        }
        List<Class<?>> interfaces = new ArrayList<>(discoveredInterfaces);
        interfaces.sort(Comparator.comparing(Class::getName));
        types.addAll(interfaces);
        return types.stream().map(MessageUrn::forClass).toList();
    }

    private static void collectInterfaces(Class<?> type, LinkedHashSet<Class<?>> interfaces) {
        for (Class<?> interfaceType : type.getInterfaces()) {
            if (interfaces.add(interfaceType)) {
                collectInterfaces(interfaceType, interfaces);
            }
        }
    }
}
