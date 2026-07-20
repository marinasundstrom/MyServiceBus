package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.mediator.MediatorTransport;

class TransportCapabilityTest {
    @Test
    void rabbitMqDescriptorDeclaresPortableCapabilities() {
        TransportCapabilityDescriptor descriptor = TransportCapabilityDescriptors.RABBITMQ;

        assertEquals(1, descriptor.version());
        assertEquals("rabbitmq", descriptor.transport());
        assertEquals(TransportCapabilitySupport.NATIVE, descriptor.get(TransportCapabilities.DIRECTED_SEND));
        assertEquals(TransportCapabilitySupport.EMULATED, descriptor.get(TransportCapabilities.REDELIVERY));
        assertEquals(TransportCapabilitySupport.UNSUPPORTED, descriptor.get(TransportCapabilities.REPLAY));
        assertEquals(TransportCapabilitySupport.UNSUPPORTED, descriptor.get("unknownCapability"));
    }

    @Test
    void mediatorExposesItsDescriptor() {
        TransportCapabilityDescriptor descriptor = MediatorTransport.capabilities();

        assertEquals("in-memory", descriptor.transport());
        assertEquals(TransportCapabilitySupport.UNSUPPORTED, descriptor.get(TransportCapabilities.DURABILITY));
    }

    @Test
    void supportValuesHaveStableProtocolNames() throws Exception {
        ObjectMapper mapper = new ObjectMapper();

        assertEquals("\"native\"", mapper.writeValueAsString(TransportCapabilitySupport.NATIVE));
        assertEquals("\"emulated\"", mapper.writeValueAsString(TransportCapabilitySupport.EMULATED));
        assertEquals("\"unsupported\"", mapper.writeValueAsString(TransportCapabilitySupport.UNSUPPORTED));
        String descriptor = mapper.writeValueAsString(TransportCapabilityDescriptors.RABBITMQ);
        assertTrue(descriptor.contains("\"version\":1"));
    }
}
