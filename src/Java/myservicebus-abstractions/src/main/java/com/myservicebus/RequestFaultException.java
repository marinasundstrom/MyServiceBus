package com.myservicebus;

import java.util.stream.Collectors;

public class RequestFaultException extends RuntimeException {
    private final Fault<?> fault;

    public RequestFaultException(String requestType, Fault<?> fault) {
        super("The " + requestType + " request faulted: " +
                fault.getExceptions().stream()
                        .map(m -> {
                            var message = m.getMessage();
                            if (message == null) {
                                message = m.getExceptionType();
                            }
                            return message;
                        })
                        .collect(Collectors.joining(System.lineSeparator())));
        this.fault = fault;
    }

    public Fault<?> getFault() {
        return fault;
    }
}
