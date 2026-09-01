package com.myservicebus.mediator;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.EntityNameFormatter;
import com.myservicebus.HandlerWithResult;
import com.myservicebus.MediatorHandlerCardinalityException;
import com.myservicebus.MediatorHandlerNotFoundException;
import com.myservicebus.MediatorResponseTypeException;
import com.myservicebus.SendContext;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.lang.reflect.TypeVariable;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;
import java.util.function.Function;

public class MediatorBus implements Mediator {
    private final ServiceProvider serviceProvider;

    public MediatorBus(ServiceProvider provider) {
        this.serviceProvider = provider;
    }

    public static MediatorBus configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configure) {
        var busRegistrationConfigurator = new BusRegistrationConfiguratorImpl(services);
        configure.accept(busRegistrationConfigurator);
        MediatorTransport.configure(busRegistrationConfigurator);
        busRegistrationConfigurator.complete();
        return new MediatorBus(services.buildServiceProvider());
    }

    @Override
    public CompletableFuture<Void> publish(Object message) {
        return publish(message, CancellationToken.none());
    }

    @Override
    public CompletableFuture<Void> publish(Object message, CancellationToken cancellationToken) {
        Objects.requireNonNull(message, "message");
        Objects.requireNonNull(cancellationToken, "cancellationToken");
        String exchange = EntityNameFormatter.format(message.getClass());
        return dispatch(endpoint -> endpoint.send(message, cancellationToken), "loopback://" + exchange);
    }

    @Override
    public CompletableFuture<Void> send(Object message) {
        return send(message, CancellationToken.none());
    }

    @Override
    public CompletableFuture<Void> send(Object message, CancellationToken cancellationToken) {
        Objects.requireNonNull(message, "message");
        Objects.requireNonNull(cancellationToken, "cancellationToken");
        ConsumerTopology handler = getSingleHandler(message.getClass());
        if (!findResultTypes(handler.getConsumerType()).isEmpty()) {
            throw new MediatorResponseTypeException(message.getClass(), Void.class, handler.getConsumerType());
        }
        return dispatch(endpoint -> endpoint.send(message, cancellationToken),
                "loopback://" + EntityNameFormatter.format(message.getClass()));
    }

    @Override
    public <TResponse> CompletableFuture<TResponse> send(Request<TResponse> request) {
        return send(request, CancellationToken.none());
    }

    @Override
    public <TResponse> CompletableFuture<TResponse> send(
            Request<TResponse> request,
            CancellationToken cancellationToken) {
        Objects.requireNonNull(request, "request");
        return send(request, Objects.requireNonNull(request.responseType(), "request.responseType()"), cancellationToken);
    }

    @Override
    public <TResponse> CompletableFuture<TResponse> send(Object message, Class<TResponse> responseType) {
        return send(message, responseType, CancellationToken.none());
    }

    @Override
    public <TResponse> CompletableFuture<TResponse> send(
            Object message,
            Class<TResponse> responseType,
            CancellationToken cancellationToken) {
        Objects.requireNonNull(message, "message");
        Objects.requireNonNull(responseType, "responseType");
        Objects.requireNonNull(cancellationToken, "cancellationToken");
        ConsumerTopology handler = getSingleHandler(message.getClass());
        List<Class<?>> resultTypes = findResultTypes(handler.getConsumerType());
        if (!resultTypes.isEmpty() && resultTypes.stream().noneMatch(responseType::isAssignableFrom)) {
            throw new MediatorResponseTypeException(message.getClass(), responseType, handler.getConsumerType());
        }
        return dispatch(endpoint -> endpoint.request(
                new SendContext(message, cancellationToken), responseType),
                "loopback://" + EntityNameFormatter.format(message.getClass()));
    }

    public CompletableFuture<Void> sendTo(String destination, Object message) {
        return sendTo(destination, message, CancellationToken.none());
    }

    public CompletableFuture<Void> sendTo(
            String destination,
            Object message,
            CancellationToken cancellationToken) {
        Objects.requireNonNull(destination, "destination");
        Objects.requireNonNull(message, "message");
        Objects.requireNonNull(cancellationToken, "cancellationToken");
        return dispatch(endpoint -> endpoint.send(message, cancellationToken), destination);
    }

    private <T> CompletableFuture<T> dispatch(
            Function<MediatorSendEndpoint, CompletableFuture<T>> operation,
            String destination) {
        ServiceScope scope = serviceProvider.createScope();
        try {
            MediatorSendEndpointProvider provider = scope.getServiceProvider()
                    .getService(MediatorSendEndpointProvider.class);
            MediatorSendEndpoint endpoint = provider.getMediatorSendEndpoint();
            CompletableFuture<T> result = operation.apply(endpoint);
            scope.detach();
            return result.whenComplete((ignored, failure) -> scope.close());
        } catch (RuntimeException exception) {
            scope.close();
            throw exception;
        }
    }

    private ConsumerTopology getSingleHandler(Class<?> messageType) {
        List<ConsumerTopology> handlers = serviceProvider.getService(TopologyRegistry.class)
                .getConsumers()
                .stream()
                .filter(consumer -> consumer.getBindings().stream()
                        .anyMatch(binding -> binding.getMessageType().isAssignableFrom(messageType)))
                .toList();
        if (handlers.isEmpty()) {
            throw new MediatorHandlerNotFoundException(messageType);
        }
        if (handlers.size() > 1) {
            throw new MediatorHandlerCardinalityException(
                    messageType,
                    handlers.stream().map(ConsumerTopology::getConsumerType).toList());
        }
        return handlers.get(0);
    }

    private static List<Class<?>> findResultTypes(Class<?> handlerType) {
        List<Class<?>> resultTypes = new ArrayList<>();
        collectResultTypes(handlerType, Map.of(), resultTypes);
        return resultTypes;
    }

    private static void collectResultTypes(
            Type type,
            Map<TypeVariable<?>, Type> bindings,
            List<Class<?>> resultTypes) {
        if (type == null || type == Object.class) {
            return;
        }

        if (type instanceof ParameterizedType parameterized) {
            if (!(parameterized.getRawType() instanceof Class<?> rawType)) {
                return;
            }

            Type[] arguments = parameterized.getActualTypeArguments();
            TypeVariable<?>[] parameters = rawType.getTypeParameters();
            Map<TypeVariable<?>, Type> nestedBindings = new HashMap<>(bindings);
            for (int i = 0; i < parameters.length; i++) {
                nestedBindings.put(parameters[i], resolveType(arguments[i], bindings));
            }

            if (rawType == HandlerWithResult.class) {
                Class<?> resultType = classFromType(resolveType(arguments[1], bindings));
                if (resultType != null) {
                    resultTypes.add(resultType);
                }
            }

            collectResultContracts(rawType, nestedBindings, resultTypes);
            return;
        }

        if (type instanceof Class<?> typeClass) {
            collectResultContracts(typeClass, bindings, resultTypes);
        }
    }

    private static void collectResultContracts(
            Class<?> type,
            Map<TypeVariable<?>, Type> bindings,
            List<Class<?>> resultTypes) {
        for (Type contract : type.getGenericInterfaces()) {
            collectResultTypes(contract, bindings, resultTypes);
        }
        collectResultTypes(type.getGenericSuperclass(), bindings, resultTypes);
    }

    private static Type resolveType(Type type, Map<TypeVariable<?>, Type> bindings) {
        Type resolved = type;
        while (resolved instanceof TypeVariable<?> variable && bindings.containsKey(variable)) {
            Type next = bindings.get(variable);
            if (next == resolved) {
                break;
            }
            resolved = next;
        }
        return resolved;
    }

    private static Class<?> classFromType(Type type) {
        if (type instanceof Class<?> typeClass) {
            return typeClass;
        }
        if (type instanceof ParameterizedType parameterized
                && parameterized.getRawType() instanceof Class<?> rawType) {
            return rawType;
        }
        return null;
    }
}
