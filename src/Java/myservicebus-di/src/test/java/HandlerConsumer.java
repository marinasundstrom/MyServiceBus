import javax.inject.Inject;
import java.util.Set;

public class HandlerConsumer {
    private final Set<Handler> handlers;

    @Inject
    public HandlerConsumer(Set<Handler> handlers) {
        this.handlers = handlers;
    }

    public Set<Handler> getHandlers() {
        return handlers;
    }
}