package com.myservicebus.di;

/**
 * Indicates the lifetime of a service within the container.
 */
public enum ServiceLifetime {
    SINGLETON,
    SCOPED,
    TRANSIENT
}

