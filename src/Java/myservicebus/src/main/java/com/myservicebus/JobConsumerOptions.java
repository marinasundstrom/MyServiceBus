package com.myservicebus;

import java.time.Duration;
import java.util.function.Consumer;

public final class JobConsumerOptions {
    private Duration jobTimeout = Duration.ofMinutes(30);
    private int concurrentJobLimit = 1;
    private int retryCount;
    private Duration retryDelay;
    private String jobTypeName;

    public JobConsumerOptions setJobTimeout(Duration timeout) {
        if (timeout == null || timeout.isZero() || timeout.isNegative()) {
            throw new IllegalArgumentException("timeout must be greater than zero");
        }
        jobTimeout = timeout;
        return this;
    }

    public JobConsumerOptions setConcurrentJobLimit(int limit) {
        if (limit <= 0) {
            throw new IllegalArgumentException("limit must be greater than zero");
        }
        concurrentJobLimit = limit;
        return this;
    }

    public JobConsumerOptions setRetry(Consumer<RetryConfigurator> configure) {
        if (configure == null) {
            throw new IllegalArgumentException("configure must not be null");
        }
        RetryConfigurator retry = new RetryConfigurator();
        configure.accept(retry);
        if (retry.getRetryCount() < 0) {
            throw new IllegalArgumentException("retry count must not be negative");
        }
        if (retry.getDelay() != null && retry.getDelay().isNegative()) {
            throw new IllegalArgumentException("retry delay must not be negative");
        }
        retryCount = retry.getRetryCount();
        retryDelay = retry.getDelay();
        return this;
    }

    public JobConsumerOptions setJobTypeName(String name) {
        if (name == null || name.isBlank()) {
            throw new IllegalArgumentException("name must not be blank");
        }
        jobTypeName = name.trim();
        return this;
    }

    public Duration getJobTimeout() {
        return jobTimeout;
    }

    public int getConcurrentJobLimit() {
        return concurrentJobLimit;
    }

    public int getRetryCount() {
        return retryCount;
    }

    public Duration getRetryDelay() {
        return retryDelay;
    }

    public String getJobTypeName() {
        return jobTypeName;
    }
}
