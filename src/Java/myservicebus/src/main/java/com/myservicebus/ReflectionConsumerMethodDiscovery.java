package com.myservicebus;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.lang.reflect.Parameter;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;

import com.myservicebus.tasks.CancellationToken;

final class ReflectionConsumerMethodDiscovery {
    private ReflectionConsumerMethodDiscovery() {
    }

    static List<Definition<?>> discover(Class<?> declaringType, boolean requireAnnotation) {
        MessageConsumer typeAnnotation = declaringType.getAnnotation(MessageConsumer.class);
        if (Consumer.class.isAssignableFrom(declaringType)) {
            typeAnnotation = null;
        }

        List<Definition<?>> definitions = new ArrayList<>();
        for (Method method : declaringType.getDeclaredMethods()) {
            MessageConsumer methodAnnotation = method.getAnnotation(MessageConsumer.class);
            if (requireAnnotation && methodAnnotation == null && typeAnnotation == null) {
                continue;
            }
            if (method.isSynthetic() || method.isBridge()) {
                continue;
            }
            definitions.add(create(method, methodAnnotation, typeAnnotation));
        }
        return definitions;
    }

    private static Definition<?> create(
            Method method,
            MessageConsumer methodAnnotation,
            MessageConsumer typeAnnotation) {
        if (!Modifier.isPublic(method.getModifiers())) {
            throw invalid(method, "method must be public");
        }
        if (method.getTypeParameters().length != 0) {
            throw invalid(method, "method must not be generic");
        }
        Class<?> returnType = method.getReturnType();
        if (returnType != void.class
                && !CompletableFuture.class.isAssignableFrom(returnType)
                && !CompletionStage.class.isAssignableFrom(returnType)) {
            throw invalid(method, "return type must be void, CompletableFuture, or CompletionStage");
        }

        Parameter[] parameters = method.getParameters();
        Binding[] bindings = new Binding[parameters.length];
        Class<?> messageType = null;
        for (int index = 0; index < parameters.length; index++) {
            Class<?> parameterType = parameters[index].getType();
            if (ConsumeContext.class.isAssignableFrom(parameterType)) {
                bindings[index] = Binding.CONTEXT;
            } else if (parameterType == CancellationToken.class) {
                bindings[index] = Binding.CANCELLATION_TOKEN;
            } else if (messageType == null) {
                messageType = parameterType;
                bindings[index] = Binding.MESSAGE;
            } else {
                bindings[index] = Binding.SERVICE;
            }
        }
        if (messageType == null || messageType.isPrimitive()) {
            throw invalid(method, "a reference-type message parameter is required");
        }
        validateContextTypes(method, messageType);

        String methodEndpointName = annotationValue(methodAnnotation);
        String typeEndpointName = annotationValue(typeAnnotation);
        String endpointName = methodEndpointName;
        if (endpointName == null) {
            endpointName = typeEndpointName;
        }
        if (endpointName == null) {
            endpointName = methodAnnotation != null
                    ? method.getName()
                    : DefaultEndpointNameFormatter.INSTANCE.format(method.getDeclaringClass());
        }

        boolean endpointNameExplicit = methodEndpointName != null || typeEndpointName != null;
        Class<?> endpointNameFormatterType = endpointNameExplicit || methodAnnotation != null
                ? null
                : method.getDeclaringClass();

        return definition(
                method,
                messageType,
                endpointName,
                endpointNameExplicit,
                endpointNameFormatterType,
                bindings);
    }

    private static void validateContextTypes(Method method, Class<?> messageType) {
        for (Type parameterType : method.getGenericParameterTypes()) {
            if (parameterType instanceof ParameterizedType parameterized
                    && parameterized.getRawType() == ConsumeContext.class
                    && parameterized.getActualTypeArguments().length == 1
                    && parameterized.getActualTypeArguments()[0] instanceof Class<?> contextMessageType
                    && contextMessageType != messageType) {
                throw invalid(method, "ConsumeContext message type must match " + messageType.getName());
            }
        }
    }

    private static String annotationValue(MessageConsumer annotation) {
        return annotation == null || annotation.value().isBlank() ? null : annotation.value();
    }

    @SuppressWarnings({ "unchecked", "rawtypes" })
    private static Definition<?> definition(
            Method method,
            Class<?> messageType,
            String endpointName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            Binding[] bindings) {
        return new Definition(
                method.getDeclaringClass(),
                messageType,
                endpointName,
                endpointNameExplicit,
                endpointNameFormatterType,
                (serviceProvider, context) -> {
                    Object target = Modifier.isStatic(method.getModifiers())
                            ? null
                            : serviceProvider.getRequiredService(method.getDeclaringClass());
                    Object[] arguments = new Object[bindings.length];
                    Parameter[] parameters = method.getParameters();
                    for (int index = 0; index < bindings.length; index++) {
                        arguments[index] = switch (bindings[index]) {
                            case MESSAGE -> context.getMessage();
                            case CONTEXT -> context;
                            case CANCELLATION_TOKEN -> context.getCancellationToken();
                            case SERVICE -> serviceProvider.getRequiredService(parameters[index].getType());
                        };
                    }
                    try {
                        Object result = method.invoke(target, arguments);
                        if (result instanceof CompletionStage<?> stage) {
                            return stage.thenApply(ignored -> (Void) null).toCompletableFuture();
                        }
                        return CompletableFuture.completedFuture(null);
                    } catch (InvocationTargetException exception) {
                        Throwable cause = exception.getCause() != null ? exception.getCause() : exception;
                        if (cause instanceof Exception checked) {
                            throw checked;
                        }
                        throw new RuntimeException(cause);
                    }
                },
                !Modifier.isStatic(method.getModifiers()));
    }

    private static IllegalArgumentException invalid(Method method, String reason) {
        return new IllegalArgumentException("Invalid consumer method " + method + ": " + reason);
    }

    record Definition<TMessage>(
            Class<?> declaringType,
            Class<TMessage> messageType,
            String endpointName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            ConsumerMethodInvoker<TMessage> invoker,
            boolean requiresInstance) {
    }

    private enum Binding {
        MESSAGE,
        CONTEXT,
        CANCELLATION_TOKEN,
        SERVICE
    }
}
