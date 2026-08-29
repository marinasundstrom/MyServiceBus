package com.myservicebus.mediator;

/** Identifies a mediator request and the response type it produces. */
public interface Request<TResponse> {
    /** Returns the runtime response type required because Java erases generic type arguments. */
    Class<TResponse> responseType();
}
