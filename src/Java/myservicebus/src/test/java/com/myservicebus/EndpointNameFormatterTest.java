package com.myservicebus;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

public class EndpointNameFormatterTest {
    static class SampleMessage {}

    @Test
    public void defaultFormatterReturnsTypeName() {
        String name = DefaultEndpointNameFormatter.INSTANCE.format(SampleMessage.class);
        assertEquals("SampleMessage", name);
    }

    @Test
    public void snakeCaseFormatterFormats() {
        String name = SnakeCaseEndpointNameFormatter.INSTANCE.format(SampleMessage.class);
        assertEquals("sample_message", name);
    }
}
