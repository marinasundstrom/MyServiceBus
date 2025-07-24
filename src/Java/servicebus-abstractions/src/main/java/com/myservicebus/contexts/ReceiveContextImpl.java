package com.myservicebus.contexts;

import java.util.Map;

import com.myservicebus.di.ServiceProvider;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class ReceiveContextImpl implements ReceiveContext {
    private byte[] body;

    private String contentType;

    private Map<String, Object> headers;

    private String messageType;

    private ServiceProvider services;
}