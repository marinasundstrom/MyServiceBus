package com.myservicebus.serialization;

import java.io.ByteArrayInputStream;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.Objects;

public final class ByteArrayMessageBody implements MessageBody {
    private final byte[] bytes;

    public ByteArrayMessageBody(byte[] bytes) {
        this.bytes = Objects.requireNonNull(bytes, "bytes");
    }

    @Override
    public Long getLength() {
        return (long) bytes.length;
    }

    @Override
    public InputStream getStream() {
        return new ByteArrayInputStream(bytes);
    }

    @Override
    public byte[] getBytes() {
        return bytes;
    }

    @Override
    public String getString() {
        return new String(bytes, StandardCharsets.UTF_8);
    }
}
