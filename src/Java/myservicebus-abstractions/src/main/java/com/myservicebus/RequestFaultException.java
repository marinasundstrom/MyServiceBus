package com.myservicebus;

public class RequestFaultException extends RuntimeException {
    public RequestFaultException(String message) {
        super(message);
    }
}
