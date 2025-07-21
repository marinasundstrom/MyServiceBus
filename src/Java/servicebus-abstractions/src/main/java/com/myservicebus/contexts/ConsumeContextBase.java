package com.myservicebus.contexts;

public interface ConsumeContextBase {
    ReceiveContext getReceiveContext();

    boolean hasMessageType(Class<?> messageType);

    // <T> boolean TryGetMessage<T>(Mutable<ConsumeContext<T>> consumeContext);
}