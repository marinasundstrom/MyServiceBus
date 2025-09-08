package com.myservicebus;

import java.lang.reflect.Field;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.util.Map;

/**
 * Utility for mapping an arbitrary object to a typed interface using
 * a dynamic proxy. Property values are resolved from either a Map or
 * via reflection on the source object's fields or getter methods.
 */
public final class MessageProxy {
    private MessageProxy() { }

    @SuppressWarnings("unchecked")
    public static <T> T create(Class<T> interfaceType, Object values) {
        if (interfaceType.isInstance(values)) {
            return (T) values;
        }

        InvocationHandler handler = (proxy, method, args) -> {
            String name = method.getName();
            if (method.getDeclaringClass() == Object.class) {
                return switch (name) {
                    case "toString" -> values.toString();
                    case "hashCode" -> values.hashCode();
                    case "equals" -> values.equals(args[0]);
                    default -> null;
                };
            }

            String property = propertyName(name);
            Object result = null;

            if (values instanceof Map<?, ?> map) {
                result = map.get(property);
            } else {
                try {
                    Method m = values.getClass().getMethod(name);
                    result = m.invoke(values);
                } catch (NoSuchMethodException ex) {
                    try {
                        Field f = values.getClass().getDeclaredField(property);
                        f.setAccessible(true);
                        result = f.get(values);
                    } catch (NoSuchFieldException e) {
                        throw new IllegalStateException("No property '" + property + "' on anonymous message");
                    }
                }
            }

            return result;
        };

        return (T) Proxy.newProxyInstance(interfaceType.getClassLoader(), new Class<?>[] { interfaceType }, handler);
    }

    private static String propertyName(String methodName) {
        if (methodName.startsWith("get") && methodName.length() > 3) {
            return Character.toLowerCase(methodName.charAt(3)) + methodName.substring(4);
        }
        if (methodName.startsWith("is") && methodName.length() > 2) {
            return Character.toLowerCase(methodName.charAt(2)) + methodName.substring(3);
        }
        return methodName;
    }
}

