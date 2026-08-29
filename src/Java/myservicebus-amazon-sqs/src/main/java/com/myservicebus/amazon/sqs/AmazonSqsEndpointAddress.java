package com.myservicebus.amazon.sqs;

import java.net.URI;

record AmazonSqsEndpointAddress(String entityName, EntityKind kind) {
    enum EntityKind { QUEUE, TOPIC }

    static AmazonSqsEndpointAddress parse(URI address) {
        String scheme = address.getScheme().toLowerCase(java.util.Locale.ROOT);
        if (!scheme.equals("amazonsqs") && !scheme.equals("queue") && !scheme.equals("topic")) {
            throw new IllegalArgumentException("Unsupported Amazon SQS address scheme: " + scheme);
        }
        String path = address.isOpaque() ? address.getSchemeSpecificPart().split("\\?", 2)[0] : address.getPath();
        String name = path.substring(path.lastIndexOf('/') + 1);
        AmazonSqsEntityNames.validate(name);
        boolean topic = scheme.equals("topic") || (address.getQuery() != null &&
                java.util.Arrays.stream(address.getQuery().split("&"))
                        .anyMatch(value -> value.equalsIgnoreCase("type=topic")));
        return new AmazonSqsEndpointAddress(name, topic ? EntityKind.TOPIC : EntityKind.QUEUE);
    }
}
