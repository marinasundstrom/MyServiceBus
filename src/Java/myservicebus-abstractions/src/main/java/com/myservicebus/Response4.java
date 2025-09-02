package com.myservicebus;

import java.util.Optional;

public class Response4<T1, T2, T3, T4> {
    private final Object message;

    private Response4(Object message) {
        this.message = message;
    }

    public static <T1, T2, T3, T4> Response4<T1, T2, T3, T4> fromT1(T1 message) {
        return new Response4<>(message);
    }

    public static <T1, T2, T3, T4> Response4<T1, T2, T3, T4> fromT2(T2 message) {
        return new Response4<>(message);
    }

    public static <T1, T2, T3, T4> Response4<T1, T2, T3, T4> fromT3(T3 message) {
        return new Response4<>(message);
    }

    public static <T1, T2, T3, T4> Response4<T1, T2, T3, T4> fromT4(T4 message) {
        return new Response4<>(message);
    }

    public <T> Optional<Response<T>> as(Class<T> type) {
        if (type.isInstance(message)) {
            return Optional.of(new Response<>(type.cast(message)));
        }
        return Optional.empty();
    }
}
