package com.myservicebus.azure.servicebus;

import java.net.URI;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;

record AzureServiceBusEndpointAddress(String entityName, EntityKind kind) {
    enum EntityKind {
        QUEUE,
        TOPIC
    }

    static AzureServiceBusEndpointAddress parse(URI address) {
        String scheme = address.getScheme();
        String entityName;
        EntityKind kind;
        if ("queue".equalsIgnoreCase(scheme)) {
            entityName = logicalName(address);
            kind = EntityKind.QUEUE;
        } else if ("topic".equalsIgnoreCase(scheme) || "exchange".equalsIgnoreCase(scheme)) {
            entityName = logicalName(address);
            kind = EntityKind.TOPIC;
        } else if ("sb".equalsIgnoreCase(scheme)) {
            String path = address.getPath();
            entityName = path == null ? "" : URLDecoder.decode(path.replaceFirst("^/", ""), StandardCharsets.UTF_8);
            String type = queryValue(address, "type");
            if (type == null || type.isBlank()) {
                kind = EntityKind.QUEUE;
            } else if ("topic".equalsIgnoreCase(type)) {
                kind = EntityKind.TOPIC;
            } else {
                throw new IllegalArgumentException(
                        "Azure Service Bus entity type is not supported: " + type);
            }
        } else {
            throw new IllegalArgumentException(
                    "Azure Service Bus address scheme is not supported: " + scheme);
        }

        if (entityName == null || entityName.isBlank()) {
            throw new IllegalArgumentException("Azure Service Bus entity name cannot be blank");
        }
        return new AzureServiceBusEndpointAddress(entityName, kind);
    }

    private static String logicalName(URI address) {
        String value = address.getSchemeSpecificPart();
        int queryIndex = value.indexOf('?');
        return queryIndex >= 0 ? value.substring(0, queryIndex) : value;
    }

    private static String queryValue(URI address, String key) {
        String query = address.getQuery();
        if (query == null) {
            return null;
        }
        for (String item : query.split("&")) {
            String[] pair = item.split("=", 2);
            if (pair.length == 2 && pair[0].equalsIgnoreCase(key)) {
                return URLDecoder.decode(pair[1], StandardCharsets.UTF_8);
            }
        }
        return null;
    }
}
