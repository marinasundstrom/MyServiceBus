package com.myservicebus;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

class Response2Test {
    @Test
    void matchSelectsFirstResponse() {
        Response2<String, Integer> response = Response2.fromT1("accepted");

        String result = response.match(value -> "first:" + value, value -> "second:" + value);

        assertEquals("first:accepted", result);
        assertEquals(Response2.First.class, response.getClass());
    }

    @Test
    void matchPreservesSecondCaseWhenResponseTypesOverlap() {
        Response2<Object, String> response = Response2.fromT2("rejected");

        String result = response.match(value -> "first:" + value, value -> "second:" + value);

        assertEquals("second:rejected", result);
        assertEquals(Response2.Second.class, response.getClass());
    }
}
