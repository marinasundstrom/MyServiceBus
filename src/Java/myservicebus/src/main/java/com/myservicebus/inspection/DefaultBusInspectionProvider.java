package com.myservicebus.inspection;

import com.myservicebus.MessageBus;
import com.myservicebus.MessageUrn;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;

import java.time.Instant;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import java.util.stream.Collectors;

public final class DefaultBusInspectionProvider implements BusInspectionProvider {
    private final MessageBus bus;

    public DefaultBusInspectionProvider(MessageBus bus) {
        this.bus = bus;
    }

    @Override
    public BusInspectionSnapshot getSnapshot() {
        String transportName = bus.getAddress().getScheme();

        List<BusInspectionSnapshot.MessageInspection> messages = bus.getTopology().getMessages().stream()
                .sorted(Comparator.comparing(com.myservicebus.topology.MessageTopology::getEntityName)
                        .thenComparing(x -> formatTypeName(x.getMessageType())))
                .map(x -> new BusInspectionSnapshot.MessageInspection(
                        formatTypeName(x.getMessageType()),
                        MessageUrn.forClass(x.getMessageType()),
                        x.getEntityName(),
                        List.of(),
                        Map.of()))
                .toList();

        List<BusInspectionSnapshot.ConsumerInspection> consumers = bus.getTopology().getConsumers().stream()
                .sorted(Comparator.comparing(ConsumerTopology::getQueueName)
                        .thenComparing(x -> formatTypeName(x.getConsumerType())))
                .map(x -> new BusInspectionSnapshot.ConsumerInspection(
                        formatTypeName(x.getConsumerType()),
                        x.getQueueName(),
                        x.getPrefetchCount(),
                        x.getSerializerClass() == null ? null : formatTypeName(x.getSerializerClass()),
                        cloneProperties(x.getQueueArguments())))
                .toList();

        List<BusInspectionSnapshot.ReceiveEndpointInspection> endpoints = bus.getTopology().getConsumers().stream()
                .collect(Collectors.groupingBy(ConsumerTopology::getQueueName, TreeMap::new, Collectors.toList()))
                .entrySet().stream()
                .map(entry -> {
                    ConsumerTopology first = entry.getValue().get(0);
                    return new BusInspectionSnapshot.ReceiveEndpointInspection(
                            entry.getKey(),
                            bus.getAddress().resolve(entry.getKey()).toString(),
                            entry.getValue().stream()
                                    .flatMap(x -> x.getBindings().stream())
                                    .collect(Collectors.toMap(
                                            x -> formatTypeName(x.getMessageType()) + "|" + x.getEntityName(),
                                            x -> x,
                                            (left, right) -> left,
                                            LinkedHashMap::new))
                                    .values().stream()
                                    .sorted(Comparator.comparing(MessageBinding::getEntityName)
                                            .thenComparing(x -> formatTypeName(x.getMessageType())))
                                    .map(x -> new BusInspectionSnapshot.MessageBindingInspection(
                                            formatTypeName(x.getMessageType()),
                                            MessageUrn.forClass(x.getMessageType()),
                                            x.getEntityName(),
                                            Map.of()))
                                    .toList(),
                            entry.getValue().stream()
                                    .map(x -> formatTypeName(x.getConsumerType()))
                                    .distinct()
                                    .sorted()
                                    .toList(),
                            buildTransportDetails(transportName, entry.getKey(), first),
                            cloneProperties(first.getQueueArguments()));
                })
                .toList();

        return new BusInspectionSnapshot(
                transportName,
                bus.getAddress(),
                Instant.now(),
                messages,
                endpoints,
                consumers);
    }

    private static BusInspectionSnapshot.TransportInspectionDetails buildTransportDetails(String transportName, String endpointName, ConsumerTopology consumer) {
        if (!"rabbitmq".equalsIgnoreCase(transportName)) {
            return null;
        }

        Map<String, Object> properties = new LinkedHashMap<>();
        properties.put("queueName", endpointName);
        properties.put("exchangeName", consumer.getBindings().isEmpty() ? endpointName : consumer.getBindings().get(0).getEntityName());
        properties.put("exchangeType", "fanout");
        properties.put("routingKey", "");
        properties.put("durable", true);
        properties.put("autoDelete", false);
        properties.put("errorQueueName", endpointName + "_error");
        properties.put("faultQueueName", endpointName + "_fault");
        properties.put("skippedQueueName", endpointName + "_skipped");
        return new BusInspectionSnapshot.TransportInspectionDetails("rabbitmq", properties);
    }

    private static Map<String, Object> cloneProperties(Map<String, Object> values) {
        if (values == null) {
            return Map.of();
        }

        Map<String, Object> clone = new LinkedHashMap<>();
        values.entrySet().stream()
                .sorted(Map.Entry.comparingByKey())
                .forEach(entry -> clone.put(entry.getKey(), entry.getValue()));
        return clone;
    }

    private static String formatTypeName(Class<?> type) {
        return type.getName();
    }
}
