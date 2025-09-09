package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.BusFactoryConfigurator;

public interface BusRegistrationConfigurator {
    <T> void addConsumer(Class<T> consumerClass);

    <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(Class<TConsumer> consumerClass,
            Class<TMessage> messageClass,
            java.util.function.Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure);

    void configureSend(java.util.function.Consumer<PipeConfigurator<SendContext>> configure);

    void configurePublish(java.util.function.Consumer<PipeConfigurator<SendContext>> configure);

    void setSerializer(Class<? extends MessageSerializer> serializerClass);

    void setDeserializer(Class<? extends MessageDeserializer> deserializerClass);

    ServiceCollection getServiceCollection();

    TopologyRegistry getTopologyRegistry();

    default <TConfigurator extends BusFactoryConfigurator> BusRegistrationConfigurator using(
            Class<TConfigurator> configuratorClass,
            java.util.function.BiConsumer<BusRegistrationContext, TConfigurator> configure) {
        throw new UnsupportedOperationException("Transport registration not supported");
    }
}
