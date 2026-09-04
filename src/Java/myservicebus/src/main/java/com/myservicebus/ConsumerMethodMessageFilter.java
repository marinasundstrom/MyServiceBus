package com.myservicebus;

import com.myservicebus.di.ServiceProvider;

/** Java projection adapter for a consumer-method invocation filter. */
public final class ConsumerMethodMessageFilter<T> extends ConsumerInvocationFilter<T> {
    public ConsumerMethodMessageFilter(ServiceProvider provider, ConsumerMethodInvoker<T> invoker) {
        super(provider, invoker);
    }
}
