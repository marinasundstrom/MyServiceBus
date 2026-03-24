package com.myservicebus.serialization;

import com.myservicebus.MessageHeaders;

public final class MassTransitHeaderConvention implements MessageHeaderConvention {
    public static final MassTransitHeaderConvention INSTANCE = new MassTransitHeaderConvention();

    private MassTransitHeaderConvention() {
    }

    @Override
    public String getContentTypeHeader() {
        return "content_type";
    }

    @Override
    public String getFaultAddressHeader() {
        return MessageHeaders.FAULT_ADDRESS;
    }

    @Override
    public boolean isHostHeader(String headerName) {
        return headerName.startsWith("MT-Host-");
    }
}
