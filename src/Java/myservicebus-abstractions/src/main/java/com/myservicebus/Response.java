package com.myservicebus;

import java.util.concurrent.atomic.AtomicReference;

public class Response<T> {
    private final T message;

    public Response(T message) {
        this.message = message;
    }

    public T getMessage() {
        return message;
    }

    public static class Two<T1, T2> {
        private final Object message;

        private Two(Object message) {
            this.message = message;
        }

        public static <T1, T2> Two<T1, T2> fromT1(T1 message) {
            return new Two<>(message);
        }

        public static <T1, T2> Two<T1, T2> fromT2(T2 message) {
            return new Two<>(message);
        }

        public <T> boolean is(Class<T> type, AtomicReference<Response<T>> holder) {
            if (type.isInstance(message)) {
                holder.set(new Response<>(type.cast(message)));
                return true;
            }
            return false;
        }
    }
}
