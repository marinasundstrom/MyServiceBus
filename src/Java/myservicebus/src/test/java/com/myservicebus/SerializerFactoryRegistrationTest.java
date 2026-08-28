package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertSame;

import java.lang.reflect.Type;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.serialization.MessageEnvelopeMode;
import com.myservicebus.serialization.MessageSerializationContext;
import com.myservicebus.serialization.MessageSerializer;

class SerializerFactoryRegistrationTest {
    @Test
    void serializerFactoriesResolveApplicationServicesWithoutReflectiveActivation() {
        ServiceCollection services = ServiceCollection.create();
        SerializerDependency dependency = new SerializerDependency();
        services.addSingleton(SerializerDependency.class, ignored -> () -> dependency);
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);

        configurator.setSerializer(provider -> new FactorySerializer(
                provider.getRequiredService(SerializerDependency.class)));
        configurator.setDeserializer(provider -> new FactoryDeserializer(
                provider.getRequiredService(SerializerDependency.class)));
        configurator.complete();

        ServiceProvider provider = services.buildServiceProvider();
        FactorySerializer serializer = (FactorySerializer) provider.getRequiredService(MessageSerializer.class);
        FactoryDeserializer deserializer = (FactoryDeserializer) provider
                .getRequiredService(MessageDeserializer.class);
        assertSame(dependency, serializer.dependency);
        assertSame(dependency, deserializer.dependency);
    }

    private static final class SerializerDependency {
    }

    private static final class FactorySerializer implements MessageSerializer {
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
        public <T> byte[] serialize(MessageSerializationContext<T> context) {
            return new byte[0];
        }
    }

    private static final class FactoryDeserializer implements MessageDeserializer {
        private final SerializerDependency dependency;

        FactoryDeserializer(SerializerDependency dependency) {
            this.dependency = dependency;
        }

        @Override
        public <T> Envelope<T> deserialize(byte[] data, Type type) {
            return null;
        }
    }
}
