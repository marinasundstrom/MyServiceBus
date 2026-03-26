package com.myservicebus.testapp.dashboard;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Filter;
import com.myservicebus.MessageUrn;
import com.myservicebus.Pipe;
import com.myservicebus.SendContext;

import java.net.URI;
import java.util.concurrent.CompletableFuture;

public final class DashboardMetricsFilters {
    private DashboardMetricsFilters() {
    }

    public static final class PublishMetricsFilter implements Filter<SendContext> {
        private final DashboardState state;

        public PublishMetricsFilter(DashboardState state) {
            this.state = state;
        }

        @Override
        public CompletableFuture<Void> send(SendContext context, Pipe<SendContext> next) {
            URI destination = context.getDestinationAddress();
            state.recordPublished(
                    destination == null ? null : DashboardSnapshotFactory.resolveMessageTypeFromDestination(destination),
                    destination == null ? null : DashboardSnapshotFactory.resolveMessageUrnFromDestination(destination));
            return next.send(context);
        }
    }

    public static final class SendMetricsFilter implements Filter<SendContext> {
        private final DashboardState state;

        public SendMetricsFilter(DashboardState state) {
            this.state = state;
        }

        @Override
        public CompletableFuture<Void> send(SendContext context, Pipe<SendContext> next) {
            URI destination = context.getDestinationAddress();
            state.recordSent(
                    destination == null ? null : DashboardSnapshotFactory.resolveMessageTypeFromDestination(destination),
                    destination == null ? null : DashboardSnapshotFactory.resolveMessageUrnFromDestination(destination));
            return next.send(context);
        }
    }

    public static final class ConsumeMetricsFilter<T> implements Filter<ConsumeContext<T>> {
        private final DashboardState state;
        private final String queueName;
        private final Class<T> messageClass;

        public ConsumeMetricsFilter(DashboardState state, String queueName, Class<T> messageClass) {
            this.state = state;
            this.queueName = queueName;
            this.messageClass = messageClass;
        }

        @Override
        public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
            DashboardState.ConsumeScope scope = state.trackConsume(queueName, messageClass.getName(), MessageUrn.forClass(messageClass));
            return next.send(context)
                    .thenRun(scope::markSuccess)
                    .whenComplete((ignored, throwable) -> {
                        if (throwable != null) {
                            scope.markFault();
                        }
                        scope.close();
                    });
        }
    }
}
