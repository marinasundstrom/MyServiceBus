package com.myservicebus.serialization;

/**
 * Optional MyServiceBus metadata for wire formats that need dispatch behavior
 * beyond the MassTransit-compatible serializer contract.
 */
public interface MessageSerializerMetadata {
    MessageEnvelopeMode getEnvelopeMode();
}
