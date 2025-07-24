package com.myservicebus;

import com.myservicebus.contexts.ReceiveContext;

public interface MessageDispatcher {

    void dispatch(Object message, ReceiveContext context);

}
