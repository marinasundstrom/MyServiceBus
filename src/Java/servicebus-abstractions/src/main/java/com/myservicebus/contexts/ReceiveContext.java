package com.myservicebus.contexts;

import java.util.Map;

import com.myservicebus.di.ServiceProvider;;

public interface ReceiveContext extends PipeContext {
    public byte[] getBody();

    public String getContentType();

    public Map<String, Object> getHeaders();

    public String getMessageType();

    public ServiceProvider getServices();
}