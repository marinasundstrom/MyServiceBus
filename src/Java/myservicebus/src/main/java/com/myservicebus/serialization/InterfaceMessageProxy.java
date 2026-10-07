package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.lang.reflect.Proxy;
import java.util.HashMap;
import java.util.Map;

final class InterfaceMessageProxy {
    static Object read(Class<?> contract, JsonNode body, ObjectMapper mapper) throws java.io.IOException {
        if (body == null || !body.isObject())
            throw new MessageDeserializationException("An interface message must be a JSON object");
        Map<String, Object> values = new HashMap<>();
        for (var method : contract.getMethods()) {
            if (method.getParameterCount() != 0 || method.getReturnType() == void.class) continue;
            String name = method.getName();
            String property = name.startsWith("get") ? java.beans.Introspector.decapitalize(name.substring(3))
                    : name.startsWith("is") ? java.beans.Introspector.decapitalize(name.substring(2)) : name;
            var annotation = method.getAnnotation(com.fasterxml.jackson.annotation.JsonProperty.class);
            if (annotation != null && !annotation.value().isBlank()) property = annotation.value();
            JsonNode value = body.get(property);
            values.put(name, mapper.readerFor(mapper.getTypeFactory().constructType(method.getGenericReturnType()))
                    .readValue((value == null ? mapper.nullNode() : value).traverse(mapper)));
        }
        return Proxy.newProxyInstance(contract.getClassLoader(), new Class<?>[] { contract }, (proxy, method, args) -> {
            if (method.getDeclaringClass() == Object.class) {
                return switch (method.getName()) {
                    case "toString" -> contract.getName() + values;
                    case "hashCode" -> System.identityHashCode(proxy);
                    case "equals" -> proxy == args[0];
                    default -> throw new UnsupportedOperationException(method.getName());
                };
            }
            if (values.containsKey(method.getName())) return values.get(method.getName());
            throw new UnsupportedOperationException("Message interfaces may only declare getters");
        });
    }
}
