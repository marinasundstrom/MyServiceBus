package com.myservicebus;

/**
 * Describes the request and response types of a result-bearing handler without
 * prescribing its asynchronous execution shape.
 *
 * <p>Language projections implement their native handler contract on top of
 * this metadata boundary. Java application handlers should normally implement
 * {@link HandlerWithResult} instead of this marker directly.</p>
 *
 * @param <TRequest> request message type
 * @param <TResponse> response message type
 */
public interface ResultHandler<TRequest, TResponse> extends MediatorHandler {
}
