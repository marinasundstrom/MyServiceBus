package com.myservicebus.serialization;

public interface MessageHeaderConvention {
    String getContentTypeHeader();

    String getFaultAddressHeader();

    boolean isHostHeader(String headerName);
}
