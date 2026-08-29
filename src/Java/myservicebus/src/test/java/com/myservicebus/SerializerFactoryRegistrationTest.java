package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertSame;

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
import com.myservicebus.serialization.InboundMessage;

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
        public MessageEnvelopeMode getEnvelopeMode() {
            return MessageEnvelopeMode.RAW;
        }

        @Override
        public InboundMessage deserialize(MessageBody body, Map<String, Object> headers) {
            return null;
        }

        @Override
        public MessageBody getMessageBody(String text) {
            return new ByteArrayMessageBody(text.getBytes(java.nio.charset.StandardCharsets.UTF_8));
        }
    }
}
