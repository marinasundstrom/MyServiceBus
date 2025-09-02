package com.myservicebus;

public class Response<T> {
    private final T message;

    public Response(T message) {
        this.message = message;
    }

    public T getMessage() {
        return message;
    }
}
