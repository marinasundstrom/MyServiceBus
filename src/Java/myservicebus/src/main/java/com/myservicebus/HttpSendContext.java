package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;

public class HttpSendContext extends SendContext {
    private String contentType = "application/json";

    public HttpSendContext(Object message, CancellationToken cancellationToken) {
        super(message, cancellationToken);
    }

    public String getContentType() {
        return contentType;
    }

    public void setContentType(String contentType) {
        this.contentType = contentType;
    }
}

