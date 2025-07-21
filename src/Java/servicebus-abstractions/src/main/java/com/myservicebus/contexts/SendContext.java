package com.myservicebus.contexts;

public interface SendContext<T> extends SendContextBase {
    T getMessage();
}