package com.myservicebus;

/**
 * Java projection contract for invoking a consumer method in the active
 * message scope.
 */
@FunctionalInterface
public interface ConsumerMethodInvoker<TMessage> extends ConsumerInvoker<TMessage> {
}
