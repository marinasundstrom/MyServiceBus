package transports;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public interface PublishEndpoint {
    <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken);
}