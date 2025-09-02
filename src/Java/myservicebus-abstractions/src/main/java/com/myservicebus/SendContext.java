package com.myservicebus;

import java.util.HashMap;
import java.util.Map;

import com.myservicebus.tasks.CancellationToken;

public class SendContext implements PipeContext {
    private Object message;
    private final Map<String, Object> headers = new HashMap<>();
    private final CancellationToken cancellationToken;

    public SendContext(Object message, CancellationToken cancellationToken) {
        this.message = message;
        this.cancellationToken = cancellationToken;
    }

    public Object getMessage() {
        return message;
    }

    public void setMessage(Object message) {
        this.message = message;
    }

    public Map<String, Object> getHeaders() {
        return headers;
    }

    @Override
    public CancellationToken getCancellationToken() {
        return cancellationToken;
    }
}
