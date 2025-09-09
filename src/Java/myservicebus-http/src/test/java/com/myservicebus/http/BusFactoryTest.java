package com.myservicebus.http;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

import java.net.URI;

import org.junit.jupiter.api.Test;

import com.myservicebus.MessageBus;

public class BusFactoryTest {
    @Test
    public void factoryBuildsBus() {
        MessageBus bus = MessageBus.factory.create(HttpFactoryConfigurator.class, cfg -> {
            cfg.host(URI.create("http://localhost:5000/"));
        });
        assertNotNull(bus);
        assertEquals(URI.create("http://localhost:5000/"), bus.getAddress());
    }
}
