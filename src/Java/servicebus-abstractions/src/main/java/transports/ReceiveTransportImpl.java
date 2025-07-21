package transports;

import com.myservicebus.contexts.PipeContext;
import com.myservicebus.contexts.ReceiveEndpointContext;

public class ReceiveTransportImpl<TContext extends PipeContext> implements ReceiveTransport {

    private final ReceiveEndpointContext context;

    public ReceiveTransportImpl(ReceiveEndpointContext context) {
        this.context = context;
    }

    @Override
    public ReceiveTransportHandle start() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'start'");
    }

}