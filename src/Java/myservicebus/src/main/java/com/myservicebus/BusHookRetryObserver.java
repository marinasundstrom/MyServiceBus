package com.myservicebus;

import java.util.Set;
import javax.inject.Inject;

import com.myservicebus.logging.LoggerFactory;

public final class BusHookRetryObserver implements RetryObserver {
    private final BusHookDispatcher dispatcher;

    @Inject
    public BusHookRetryObserver(Set<BusHook> hooks, LoggerFactory loggerFactory) {
        dispatcher = new BusHookDispatcher(hooks, loggerFactory);
    }

    @Override
    public void observe(RetryEvent retryEvent) {
        if (!dispatcher.isEnabled() || !(retryEvent.context() instanceof ConsumeContext<?> context)) {
            return;
        }

        Object message = context.getMessage();
        dispatcher.dispatch(MessageOperationHookEvent.create(
                retryEvent.exhausted() ? "retry_exhausted" : "retry_attempted",
                false,
                message.getClass(),
                null,
                null,
                System.nanoTime(),
                retryEvent.exception(),
                context.getCorrelationId() == null ? null : context.getCorrelationId().toString(),
                context.getConversationId() == null ? null : context.getConversationId().toString(),
                retryEvent.attempt(),
                retryEvent.retryLimit(),
                context.getMessageId() == null ? null : context.getMessageId().toString()));
    }
}
