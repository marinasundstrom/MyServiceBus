package com.myservicebus.rabbitmq;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import com.rabbitmq.client.Connection;
import org.junit.jupiter.api.Test;

class RabbitMqHealthCheckTest {
    @Test
    void reportsHealthyWhenConnectionOpen() throws Exception {
        ConnectionProvider provider = mock(ConnectionProvider.class);
        Connection connection = mock(Connection.class);
        when(connection.isOpen()).thenReturn(true);
        when(provider.getOrCreateConnection()).thenReturn(connection);

        RabbitMqHealthCheck check = new RabbitMqHealthCheck(provider);
        assertTrue(check.isHealthy());
    }

    @Test
    void reportsUnhealthyOnException() throws Exception {
        ConnectionProvider provider = mock(ConnectionProvider.class);
        when(provider.getOrCreateConnection()).thenThrow(new Exception("fail"));

        RabbitMqHealthCheck check = new RabbitMqHealthCheck(provider);
        assertFalse(check.isHealthy());
    }
}
