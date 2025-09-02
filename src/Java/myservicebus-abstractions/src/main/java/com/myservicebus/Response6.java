package com.myservicebus;

import java.util.Optional;

public class Response6<T1, T2, T3, T4, T5, T6> {
    private final Object message;

    private Response6(Object message) {
        this.message = message;
    }

    public static <T1, T2, T3, T4, T5, T6> Response6<T1, T2, T3, T4, T5, T6> fromT1(T1 message) {
        return new Response6<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6> Response6<T1, T2, T3, T4, T5, T6> fromT2(T2 message) {
        return new Response6<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6> Response6<T1, T2, T3, T4, T5, T6> fromT3(T3 message) {
        return new Response6<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6> Response6<T1, T2, T3, T4, T5, T6> fromT4(T4 message) {
        return new Response6<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6> Response6<T1, T2, T3, T4, T5, T6> fromT5(T5 message) {
        return new Response6<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6> Response6<T1, T2, T3, T4, T5, T6> fromT6(T6 message) {
        return new Response6<>(message);
    }

    public <T> Optional<Response<T>> as(Class<T> type) {
        if (type.isInstance(message)) {
            return Optional.of(new Response<>(type.cast(message)));
        }
        return Optional.empty();
    }
}
