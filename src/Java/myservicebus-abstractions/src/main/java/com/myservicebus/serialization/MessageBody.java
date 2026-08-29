package com.myservicebus.serialization;

import java.io.InputStream;

public interface MessageBody {
    Long getLength();

    InputStream getStream();

    byte[] getBytes();

    String getString();
}
