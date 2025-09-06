package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

class RequestFaultExceptionTest {
    static class Ping {
    }

    @Test
    void usesExceptionTypeWhenMessageMissing() {
        Fault<Ping> fault = new Fault<>();
        fault.setMessage(new Ping());
        ExceptionInfo info = new ExceptionInfo();
        info.setExceptionType("java.lang.RuntimeException");
        info.setMessage(null);
        fault.setExceptions(java.util.List.of(info));

        RequestFaultException ex = new RequestFaultException("Ping", fault);
        assertTrue(ex.getMessage().contains("java.lang.RuntimeException"));
    }
}
