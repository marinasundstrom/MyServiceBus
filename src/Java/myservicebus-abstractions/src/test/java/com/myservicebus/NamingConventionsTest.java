package com.myservicebus;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

public class NamingConventionsTest {
    static class SampleUrnMessage { }

    @Test
    void getMessageUrnReturnsExpected() {
        String urn = NamingConventions.getMessageUrn(SampleUrnMessage.class);
        assertEquals("urn:message:TestApp:SampleUrnMessage", urn);
    }
}
