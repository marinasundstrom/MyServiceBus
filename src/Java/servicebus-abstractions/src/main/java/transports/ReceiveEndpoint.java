package transports;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public interface ReceiveEndpoint {
    CompletableFuture<Void> start(CancellationToken cancellationToken);

    CompletableFuture<Void> stop(CancellationToken cancellationToken);
}
