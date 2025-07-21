package com.myservicebus.contexts;

import java.net.URI;

public interface SendContextBase extends PipeContext {
    URI getDestinationAddress();
}