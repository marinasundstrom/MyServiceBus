package com.myservicebus;

import java.lang.reflect.*;
import java.util.HashMap;
import java.util.Map;

public final class AnonymousMessageFactory {
    private AnonymousMessageFactory() {
    }

    @SuppressWarnings("unchecked")
    public static <T> T create(Class<T> interfaceType, Object values) {
        if (interfaceType.isInstance(values)) {
            return (T) values;
        }

        Map<String, Method> props = new HashMap<>();
        for (Method m : values.getClass().getMethods()) {
            if (m.getParameterCount() == 0 && m.getName().startsWith("get")) {
                props.put(m.getName().substring(3), m);
            }
        }

        InvocationHandler handler = (proxy, method, args) -> {
            if (method.getParameterCount() == 0 && method.getName().startsWith("get")) {
                Method src = props.get(method.getName().substring(3));
                if (src != null) {
                    return src.invoke(values);
                }
                Class<?> returnType = method.getReturnType();
                if (returnType.isPrimitive()) {
                    if (returnType == boolean.class) return false;
                    if (returnType == char.class) return '\0';
                    return 0;
                }
                return null;
            }
            throw new UnsupportedOperationException(method.getName());
        };

        return (T) Proxy.newProxyInstance(interfaceType.getClassLoader(), new Class<?>[] { interfaceType }, handler);
    }
}
