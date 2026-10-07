package com.myservicebus;

import com.myservicebus.serialization.*;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

class ContractIntegrationTest {
    @MessageUrnName(value = "urn:message:Acme:Order", useDefaultPrefix = false)
    record LocalOrder(int quantity) { }
    @MessageUrnName("Acme:ShortOrder")
    record ShortOrder(int quantity) { }
    interface Order { int getQuantity(); }

    @Test
    void identityOverridesAreExplicitAndIsolated() {
        assertEquals("urn:message:Acme:Order", MessageUrn.forClass(LocalOrder.class));
        assertEquals("urn:message:Acme:ShortOrder", MessageUrn.forClass(ShortOrder.class));
        var first = new MessageContractRegistry();
        var second = new MessageContractRegistry();
        first.setMessageUrn(LocalOrder.class, "urn:message:Acme:Alternate");
        first.freeze(List.of(LocalOrder.class));
        assertNotEquals(first.getMessageUrn(LocalOrder.class), second.getMessageUrn(LocalOrder.class));
        assertThrows(IllegalStateException.class, () -> first.setMessageUrn(LocalOrder.class, "urn:late"));
        second.setMessageUrn(Order.class, MessageUrn.forClass(LocalOrder.class));
        assertThrows(IllegalStateException.class, () -> second.freeze(List.of(LocalOrder.class, Order.class)));
        assertFalse(MessageUrn.forMessageTypes(LocalOrder.class).stream().anyMatch(urn -> urn.contains("java.lang:Record")));
    }

}
