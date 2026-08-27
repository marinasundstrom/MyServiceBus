package com.myservicebus;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

public class EndpointNameFormatterTest {
    static class SampleMessage {}
    static class SubmitOrderConsumer {}

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

    @Test
    public void formattersTrimMassTransitEndpointSuffixes() {
        assertEquals("SubmitOrder", DefaultEndpointNameFormatter.INSTANCE.format(SubmitOrderConsumer.class));
        assertEquals("submit-order", KebabCaseEndpointNameFormatter.INSTANCE.format(SubmitOrderConsumer.class));
        assertEquals("submit_order", SnakeCaseEndpointNameFormatter.INSTANCE.format(SubmitOrderConsumer.class));
    }
}
