package transports;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.SendContext;
import com.myservicebus.middleware.Pipe;
import com.myservicebus.tasks.CancellationToken;

public interface SendTransport {
    <T> CompletableFuture<SendContext<T>> createSendContext(T message, Pipe<SendContext<T>> pipe,
            CancellationToken cancellationToken);

    <T> CompletableFuture<Void> send(T message, Pipe<SendContext<T>> pipe, CancellationToken cancellationToken);
}