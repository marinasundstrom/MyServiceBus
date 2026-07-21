package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.concurrent.CompletionException;
import com.myservicebus.mediator.MediatorTransport;
import com.myservicebus.di.ServiceCollection;

class TransportCapabilityTest {
    @Test
    void rabbitMqDescriptorDeclaresPortableCapabilities() {
        TransportCapabilityDescriptor descriptor = TransportCapabilityDescriptors.RABBITMQ;

        assertEquals(1, descriptor.version());
        assertEquals("rabbitmq", descriptor.transport());
        assertEquals(TransportCapabilitySupport.NATIVE, descriptor.get(TransportCapabilities.DIRECTED_SEND));
        assertEquals(TransportCapabilitySupport.EMULATED, descriptor.get(TransportCapabilities.RETRY));
        assertEquals(TransportCapabilitySupport.UNSUPPORTED, descriptor.get(TransportCapabilities.REDELIVERY));
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

    @Test
    void startupRejectsAnUnsupportedRequiredCapability() {
        MessageBus bus = MessageBusImpl.configure(ServiceCollection.create(), configurator -> {
            configurator.requireTransportCapability(TransportCapabilities.DURABILITY);
            MediatorTransport.configure(configurator);
        });

        UnsupportedTransportCapabilityException exception = org.junit.jupiter.api.Assertions.assertThrows(
                UnsupportedTransportCapabilityException.class,
                bus::start);

        assertEquals("in-memory", exception.getTransport());
        assertEquals(TransportCapabilities.DURABILITY, exception.getCapability());
        CompletionException publishFailure = org.junit.jupiter.api.Assertions.assertThrows(
                CompletionException.class,
                () -> bus.publish(new Object()).join());
        assertInstanceOf(IllegalStateException.class, publishFailure.getCause());
    }

    @Test
    void startupAcceptsEmulationUnlessNativeSupportIsRequired() throws Exception {
        MessageBus availableBus = MessageBusImpl.configure(ServiceCollection.create(), configurator -> {
            configurator.requireTransportCapability(TransportCapabilities.SCHEDULING);
            MediatorTransport.configure(configurator);
        });
        availableBus.start();
        availableBus.stop();

        MessageBus nativeBus = MessageBusImpl.configure(ServiceCollection.create(), configurator -> {
            configurator.requireTransportCapability(TransportCapabilities.SCHEDULING, true);
            MediatorTransport.configure(configurator);
        });

        org.junit.jupiter.api.Assertions.assertThrows(
                UnsupportedTransportCapabilityException.class,
                nativeBus::start);
    }
}
