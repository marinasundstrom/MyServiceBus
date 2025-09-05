package com.myservicebus;

import java.util.Arrays;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.stream.Collectors;

import com.myservicebus.tasks.CancellationToken;

public class ErrorTransportFilter<T> implements Filter<ConsumeContext<T>> {
    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        return next.send(context).handle((v, ex) -> {
            if (ex != null) {
                Throwable cause = ex instanceof CompletionException && ex.getCause() != null ? ex.getCause() : ex;
                String errorAddress = context.getErrorAddress();
                if (errorAddress != null) {
                    SendEndpoint endpoint = context.getSendEndpoint(errorAddress);
                    int tmp = 0;
                    Object value = context.getHeaders().get(MessageHeaders.REDELIVERY_COUNT);
                    if (value instanceof Number n)
                        tmp = n.intValue();

                    final int redelivery = tmp;
                    final HostInfo host = HostInfoProvider.capture();
                    endpoint.send(context.getMessage(), sendCtx -> {
                        sendCtx.getHeaders().put(MessageHeaders.EXCEPTION_TYPE, cause.getClass().getName());
                        sendCtx.getHeaders().put(MessageHeaders.EXCEPTION_MESSAGE, cause.getMessage());
                        sendCtx.getHeaders().put(MessageHeaders.EXCEPTION_STACKTRACE,
                                Arrays.stream(cause.getStackTrace()).map(Object::toString)
                                        .collect(Collectors.joining("\n")));
                        sendCtx.getHeaders().put(MessageHeaders.REASON, "fault");
                        sendCtx.getHeaders().put(MessageHeaders.REDELIVERY_COUNT, redelivery);
                        sendCtx.getHeaders().put(MessageHeaders.HOST_MACHINE, host.getMachineName());
                        sendCtx.getHeaders().put(MessageHeaders.HOST_PROCESS, host.getProcessName());
                        sendCtx.getHeaders().put(MessageHeaders.HOST_PROCESS_ID, host.getProcessId());
                        sendCtx.getHeaders().put(MessageHeaders.HOST_ASSEMBLY, host.getAssembly());
                        sendCtx.getHeaders().put(MessageHeaders.HOST_ASSEMBLY_VERSION, host.getAssemblyVersion());
                        sendCtx.getHeaders().put(MessageHeaders.HOST_FRAMEWORK_VERSION, host.getFrameworkVersion());
                        sendCtx.getHeaders().put(MessageHeaders.HOST_MASS_TRANSIT_VERSION, host.getMassTransitVersion());
                        sendCtx.getHeaders().put(MessageHeaders.HOST_OS_VERSION, host.getOperatingSystemVersion());
                    }, CancellationToken.none).join();
                }
                throw new CompletionException(cause);
            }
            return null;
        });
    }
}
