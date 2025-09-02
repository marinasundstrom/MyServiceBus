package com.myservicebus.rabbitmq;

import java.util.Map;

public class MessageConfigurator<T> {
    private final Class<T> clz;
    private final Map<Class<?>, String> exchangeNames;

    public MessageConfigurator(Class<T> clz, Map<Class<?>, String> exchangeNames) {
        this.clz = clz;
        this.exchangeNames = exchangeNames;
    }

    public void setEntityName(String name) {
        exchangeNames.put(clz, name);
    }
}
