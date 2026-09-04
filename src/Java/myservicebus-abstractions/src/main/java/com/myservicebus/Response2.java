package com.myservicebus;

import java.util.Optional;
import java.util.function.Function;

/** A response containing exactly one of two possible message types. */
public sealed interface Response2<T1, T2> permits Response2.First, Response2.Second {
    static <T1, T2> Response2<T1, T2> fromT1(T1 message) {
        return new First<>(message);
    }

    static <T1, T2> Response2<T1, T2> fromT2(T2 message) {
        return new Second<>(message);
    }

    /** Selects the function for the response case that was received. */
    <TResult> TResult match(
            Function<? super T1, ? extends TResult> onT1,
            Function<? super T2, ? extends TResult> onT2);

    /**
     * Inspects the response by runtime message type.
     *
     * <p>Prefer the nominal cases or {@link #match(Function, Function)} when response
     * types overlap and case identity matters.</p>
     */
    <T> Optional<Response<T>> as(Class<T> type);

    /** The first declared response case. */
    record First<T1, T2>(T1 message) implements Response2<T1, T2> {
        @Override
        public <TResult> TResult match(
                Function<? super T1, ? extends TResult> onT1,
                Function<? super T2, ? extends TResult> onT2) {
            return onT1.apply(message);
        }

        @Override
        public <T> Optional<Response<T>> as(Class<T> type) {
            return type.isInstance(message)
                    ? Optional.of(new Response<>(type.cast(message)))
                    : Optional.empty();
        }
    }

    /** The second declared response case. */
    record Second<T1, T2>(T2 message) implements Response2<T1, T2> {
        @Override
        public <TResult> TResult match(
                Function<? super T1, ? extends TResult> onT1,
                Function<? super T2, ? extends TResult> onT2) {
            return onT2.apply(message);
        }

        @Override
        public <T> Optional<Response<T>> as(Class<T> type) {
            return type.isInstance(message)
                    ? Optional.of(new Response<>(type.cast(message)))
                    : Optional.empty();
        }
    }
}
