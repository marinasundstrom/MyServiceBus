package com.myservicebus;

import java.util.Optional;

public class Response7<T1, T2, T3, T4, T5, T6, T7> {
    private final Object message;

    private Response7(Object message) {
        this.message = message;
    }

    public static <T1, T2, T3, T4, T5, T6, T7> Response7<T1, T2, T3, T4, T5, T6, T7> fromT1(T1 message) {
        return new Response7<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6, T7> Response7<T1, T2, T3, T4, T5, T6, T7> fromT2(T2 message) {
        return new Response7<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6, T7> Response7<T1, T2, T3, T4, T5, T6, T7> fromT3(T3 message) {
        return new Response7<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6, T7> Response7<T1, T2, T3, T4, T5, T6, T7> fromT4(T4 message) {
        return new Response7<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6, T7> Response7<T1, T2, T3, T4, T5, T6, T7> fromT5(T5 message) {
        return new Response7<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6, T7> Response7<T1, T2, T3, T4, T5, T6, T7> fromT6(T6 message) {
        return new Response7<>(message);
    }

    public static <T1, T2, T3, T4, T5, T6, T7> Response7<T1, T2, T3, T4, T5, T6, T7> fromT7(T7 message) {
        return new Response7<>(message);
    }

    public <T> Optional<Response<T>> as(Class<T> type) {
        if (type.isInstance(message)) {
            return Optional.of(new Response<>(type.cast(message)));
        }
        return Optional.empty();
    }
}
