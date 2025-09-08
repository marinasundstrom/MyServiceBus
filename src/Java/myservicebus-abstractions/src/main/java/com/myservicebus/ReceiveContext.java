package com.myservicebus;

import java.net.URI;
import java.util.List;
import java.util.Map;
import java.util.UUID;

/**
 * Context available to message receive pipeline components.
 */
public interface ReceiveContext extends PipeContext {
    UUID getMessageId();
    List<String> getMessageType();
    URI getResponseAddress();
    URI getFaultAddress();
    URI getErrorAddress();
    Map<String, Object> getHeaders();
}
