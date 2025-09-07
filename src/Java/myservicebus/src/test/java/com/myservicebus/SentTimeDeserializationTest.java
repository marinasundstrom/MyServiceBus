package com.myservicebus;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;

import static org.junit.jupiter.api.Assertions.*;

public class SentTimeDeserializationTest {
    static class Dummy {
        public String value;
    }

    @Test
    public void parsesSentTimeWithFiveFractionalDigits() throws Exception {
        String json = "{" +
                "\"messageId\":\"00000000-0000-0000-0000-000000000001\"," +
                "\"sentTime\":\"2025-09-07T23:35:12.25925+02:00\"," +
                "\"messageType\":[\"urn:message:Dummy\"]," +
                "\"message\":{" +
                    "\"value\":\"x\"" +
                "}" +
            "}";
        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        Envelope<Dummy> envelope = mapper.readValue(json,
                mapper.getTypeFactory().constructParametricType(Envelope.class, Dummy.class));
        assertEquals(OffsetDateTime.parse("2025-09-07T23:35:12.25925+02:00").toInstant(),
                envelope.getSentTime().toInstant());
    }
}
