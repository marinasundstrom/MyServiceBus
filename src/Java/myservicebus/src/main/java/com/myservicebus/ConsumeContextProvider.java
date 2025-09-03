package com.myservicebus;

public class ConsumeContextProvider {
    private final ThreadLocal<ConsumeContext<?>> current = new ThreadLocal<>();

    public ConsumeContext<?> getContext() {
        return current.get();
    }

    public void setContext(ConsumeContext<?> context) {
        current.set(context);
    }

    public void clear() {
        current.remove();
    }
}
