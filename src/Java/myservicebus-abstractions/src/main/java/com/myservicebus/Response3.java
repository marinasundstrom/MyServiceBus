package com.myservicebus;

import java.util.Optional;

public class Response3<T1, T2, T3> {
    private final Object message;

    private Response3(Object message) {
        this.message = message;
    }

    public static <T1, T2, T3> Response3<T1, T2, T3> fromT1(T1 message) {
        return new Response3<>(message);
    }

    public static <T1, T2, T3> Response3<T1, T2, T3> fromT2(T2 message) {
        return new Response3<>(message);
    }

    public static <T1, T2, T3> Response3<T1, T2, T3> fromT3(T3 message) {
        return new Response3<>(message);
    }

    public <T> Optional<Response<T>> as(Class<T> type) {
        if (type.isInstance(message)) {
            return Optional.of(new Response<>(type.cast(message)));
        }
        return Optional.empty();
    }
}
