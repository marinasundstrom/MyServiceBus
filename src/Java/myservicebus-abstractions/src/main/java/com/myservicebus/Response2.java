package com.myservicebus;

import java.util.Optional;

public class Response2<T1, T2> {
    private final Object message;

    private Response2(Object message) {
        this.message = message;
    }

    public static <T1, T2> Response2<T1, T2> fromT1(T1 message) {
        return new Response2<>(message);
    }

    public static <T1, T2> Response2<T1, T2> fromT2(T2 message) {
        return new Response2<>(message);
    }

    public <T> Optional<Response<T>> as(Class<T> type) {
        if (type.isInstance(message)) {
            return Optional.of(new Response<>(type.cast(message)));
        }
        return Optional.empty();
    }
}
