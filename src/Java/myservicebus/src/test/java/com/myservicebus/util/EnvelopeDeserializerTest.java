package com.myservicebus.util;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.*;
import java.util.*;
import java.util.UUID;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

public class EnvelopeDeserializerTest {
    static class InnerMessage {
        private String text;
        public InnerMessage() {}
        public InnerMessage(String text) { this.text = text; }
        public String getText() { return text; }
        public void setText(String text) { this.text = text; }
    }

    @Test
    public void deserializeAndUnwrapFaultReturnsInnerMessage() throws Exception {
        InnerMessage inner = new InnerMessage("oops");

        ExceptionInfo info = new ExceptionInfo();
        info.setExceptionType("java.lang.RuntimeException");
        info.setMessage("boom");

        Fault<InnerMessage> fault = new Fault<>();
        fault.setMessage(inner);
        fault.setExceptions(List.of(info));

        Envelope<Fault<InnerMessage>> envelope = new Envelope<>();
        envelope.setMessageId(UUID.randomUUID());
        String faultUrn = "urn:message:Fault`1[[" + InnerMessage.class.getName() + ", assembly]]";
        envelope.setMessageType(List.of(faultUrn, NamingConventions.getMessageUrn(InnerMessage.class)));
        envelope.setMessage(fault);

        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        String json = mapper.writeValueAsString(envelope);

        Object result = EnvelopeDeserializer.deserializeAndUnwrapFault(json);
        Assertions.assertTrue(result instanceof InnerMessage);
        Assertions.assertEquals("oops", ((InnerMessage) result).getText());
    }
}
