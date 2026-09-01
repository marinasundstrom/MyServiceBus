package com.myservicebus.amazon.sqs;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.MessageSerializer;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.sns.SnsClient;
import software.amazon.awssdk.services.sqs.SqsClient;

import java.net.URI;

public final class AmazonSqsTransport {
    private AmazonSqsTransport() { }

    public static void configure(BusRegistrationConfigurator registration,
            AmazonSqsFactoryConfigurator configurator) {
        ServiceCollection services = registration.getServiceCollection();
        services.addSingleton(AmazonSqsFactoryConfigurator.class, provider -> () -> configurator);
        services.addSingleton(TransportCapabilityDescriptor.class,
                provider -> () -> TransportCapabilityDescriptors.AMAZON_SQS);
        services.addSingleton(URI.class,
                provider -> () -> URI.create("amazonsqs://" + configurator.getRegion() + "/"));
        services.addSingleton(SqsClient.class, provider -> () -> createSqs(configurator));
        services.addSingleton(SnsClient.class, provider -> () -> createSns(configurator));
        services.addSingleton(AmazonSqsTransportFactory.class, provider -> () -> new AmazonSqsTransportFactory(
                provider.getService(SqsClient.class), provider.getService(SnsClient.class), configurator,
                provider.getService(LoggerFactory.class)));
        services.addSingleton(TransportFactory.class,
                provider -> () -> provider.getService(AmazonSqsTransportFactory.class));
        services.addSingleton(SendContextFactory.class, provider -> () -> new DefaultSendContextFactory());
        services.addSingleton(PublishContextFactory.class, provider -> () -> new DefaultPublishContextFactory());
        services.addSingleton(AmazonSqsSendEndpointProvider.class, provider -> () ->
                new AmazonSqsSendEndpointProvider(provider.getService(AmazonSqsTransportFactory.class),
                        provider.getService(SendPipe.class), provider.getService(MessageSerializer.class),
                        provider.getService(URI.class), provider.getService(SendContextFactory.class)));
        services.addSingleton(TransportSendEndpointProvider.class,
                provider -> () -> provider.getService(AmazonSqsSendEndpointProvider.class));
        services.addSingleton(RequestClientTransport.class, provider -> () -> new TransportRequestClientTransport(
                provider.getService(TransportFactory.class), provider.getService(MessageSerializer.class),
                provider.getService(com.myservicebus.serialization.InboundMessageResolver.class)));
        services.addScoped(ScopedClientFactory.class, provider -> () ->
                new RequestClientFactory(
                        provider.getService(RequestClientTransport.class),
                        provider.getServices(com.myservicebus.BusHook.class),
                        provider.getService(com.myservicebus.logging.LoggerFactory.class)));
    }

    public static void configure(BusRegistrationConfigurator registration) {
        configure(registration, new AmazonSqsFactoryConfigurator());
    }

    private static SqsClient createSqs(AmazonSqsFactoryConfigurator configurator) {
        var builder = SqsClient.builder().region(Region.of(configurator.getRegion()));
        if (configurator.getServiceEndpoint() != null) builder.endpointOverride(configurator.getServiceEndpoint())
                .credentialsProvider(StaticCredentialsProvider.create(AwsBasicCredentials.create("test", "test")));
        return builder.build();
    }

    private static SnsClient createSns(AmazonSqsFactoryConfigurator configurator) {
        var builder = SnsClient.builder().region(Region.of(configurator.getRegion()));
        if (configurator.getServiceEndpoint() != null) builder.endpointOverride(configurator.getServiceEndpoint())
                .credentialsProvider(StaticCredentialsProvider.create(AwsBasicCredentials.create("test", "test")));
        return builder.build();
    }
}
