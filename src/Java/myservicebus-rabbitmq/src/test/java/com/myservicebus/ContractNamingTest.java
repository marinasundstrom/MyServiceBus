package com.myservicebus;

import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.assertEquals;

class ContractNamingTest {
    record NamingMessage(String value) { }
    @Test
    void entityFormattersAreLocalToEachBus() {
        String original = EntityNameFormatter.format(NamingMessage.class);
        var first = new RabbitMqFactoryConfigurator();
        var second = new RabbitMqFactoryConfigurator();
        first.setEntityNameFormatter(type -> "first-" + type.getSimpleName());
        second.setEntityNameFormatter(type -> "second-" + type.getSimpleName());
        assertEquals("first-NamingMessage", first.getEntityName(NamingMessage.class));
        assertEquals("second-NamingMessage", second.getEntityName(NamingMessage.class));
        assertEquals(original, EntityNameFormatter.format(NamingMessage.class));
        first.message(NamingMessage.class, message -> message.setEntityName("explicit"));
        assertEquals("explicit", first.getEntityName(NamingMessage.class));
        assertEquals("second-NamingMessage", second.getEntityName(NamingMessage.class));
    }
}
