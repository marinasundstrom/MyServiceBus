package com.myservicebus;

import java.time.Duration;

public final class RequestTimeout {
    private static final Duration DEFAULT_DURATION = Duration.ofSeconds(30);

    private final Duration duration;

    public RequestTimeout(Duration duration) {
        this.duration = duration;
    }

    public Duration getDuration() {
        return duration;
    }

    public static final RequestTimeout NONE = new RequestTimeout(Duration.ZERO);
    public static final RequestTimeout DEFAULT = new RequestTimeout(DEFAULT_DURATION);

    public static RequestTimeout after(Duration duration) {
        return new RequestTimeout(duration);
    }
}
