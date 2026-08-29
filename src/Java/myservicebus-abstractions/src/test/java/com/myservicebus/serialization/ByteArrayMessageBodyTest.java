package com.myservicebus.serialization;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;

import java.nio.charset.StandardCharsets;
import org.junit.jupiter.api.Test;

class ByteArrayMessageBodyTest {
    @Test
    void exposesLengthBytesStreamAndText() throws Exception {
        byte[] bytes = "hello".getBytes(StandardCharsets.UTF_8);
        ByteArrayMessageBody body = new ByteArrayMessageBody(bytes);

        assertEquals(bytes.length, body.getLength());
        assertSame(bytes, body.getBytes());
        assertEquals("hello", body.getString());
        assertArrayEquals(bytes, body.getStream().readAllBytes());
    }
}
