package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.Map;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.serialization.ByteArrayMessageBody;
import com.myservicebus.serialization.MessageBody;
import com.myservicebus.serialization.MessageEnvelopeMode;
import com.myservicebus.serialization.MessageSerializationContext;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.serialization.MessageSerializerMetadata;
import com.myservicebus.serialization.InboundMessage;
import com.myservicebus.serialization.InboundMessageResolver;
import com.myservicebus.serialization.SerializerFactory;
import com.myservicebus.TransportMessage;

class SerializerFactoryRegistrationTest {
    @Test
    void serializerFactoryConfiguresSerializerAndDeserializerWithoutReflectiveActivation() throws Exception {
        ServiceCollection services = ServiceCollection.create();
        SerializerDependency dependency = new SerializerDependency();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);

        SerializerFactory factory = new FactorySerializerFactory(dependency);
        configurator.clearSerialization();
        configurator.addSerializer(factory, true);
        configurator.addDeserializer(factory, true);
        configurator.complete();

        ServiceProvider provider = services.buildServiceProvider();
        FactorySerializer serializer = (FactorySerializer) provider.getRequiredService(MessageSerializer.class);
        FactoryDeserializer deserializer = (FactoryDeserializer) provider
                .getRequiredService(MessageDeserializer.class);
        assertSame(dependency, serializer.dependency);
        assertSame(dependency, deserializer.dependency);
        InboundMessageResolver resolver = provider.getRequiredService(InboundMessageResolver.class);
        assertInstanceOf(InboundMessageResolver.class, resolver);
        assertNull(resolver.resolve(new TransportMessage(new byte[0], Map.of())));
        assertEquals(1, dependency.deserializeCalls);
    }

    private static final class SerializerDependency {
        int deserializeCalls;
    }

    private static final class FactorySerializerFactory implements SerializerFactory {
        private final SerializerDependency dependency;

        FactorySerializerFactory(SerializerDependency dependency) {
            this.dependency = dependency;
        }

        @Override
        public String getContentType() {
            return "application/factory-test";
        }

        @Override
        public MessageSerializer createSerializer() {
            return new FactorySerializer(dependency);
        }

        @Override
        public MessageDeserializer createDeserializer() {
            return new FactoryDeserializer(dependency);
        }
    }

    private static final class FactorySerializer implements MessageSerializer, MessageSerializerMetadata {
        private final SerializerDependency dependency;

        FactorySerializer(SerializerDependency dependency) {
            this.dependency = dependency;
        }

        @Override
        public String getContentType() {
            return "application/factory-test";
        }

        @Override
        public MessageEnvelopeMode getEnvelopeMode() {
            return MessageEnvelopeMode.RAW;
        }

        @Override
        public <T> MessageBody getMessageBody(MessageSerializationContext<T> context) {
            return new ByteArrayMessageBody(new byte[0]);
        }
    }

    private static final class FactoryDeserializer implements MessageDeserializer {
        private final SerializerDependency dependency;

        FactoryDeserializer(SerializerDependency dependency) {
            this.dependency = dependency;
        }

        @Override
        public String getContentType() {
            return "application/factory-test";
        }

        @Override
        public InboundMessage deserialize(MessageBody body, Map<String, Object> headers) {
            dependency.deserializeCalls++;
            return null;
        }

        @Override
        public MessageBody getMessageBody(String text) {
            return new ByteArrayMessageBody(text.getBytes(java.nio.charset.StandardCharsets.UTF_8));
        }
    }
}
