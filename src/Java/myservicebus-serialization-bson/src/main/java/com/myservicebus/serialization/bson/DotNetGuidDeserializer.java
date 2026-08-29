package com.myservicebus.serialization.bson;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.core.JsonToken;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.util.UUID;

final class DotNetGuidDeserializer extends JsonDeserializer<UUID> {
    @Override
    public UUID deserialize(JsonParser parser, DeserializationContext context) throws IOException {
        if (parser.currentToken() == JsonToken.VALUE_STRING) {
            return UUID.fromString(parser.getValueAsString());
        }
        if (parser.currentToken() != JsonToken.VALUE_EMBEDDED_OBJECT) {
            return (UUID) context.handleUnexpectedToken(UUID.class, parser);
        }

        byte[] dotNetBytes = parser.getBinaryValue();
        if (dotNetBytes.length != 16) {
            return (UUID) context.handleWeirdStringValue(
                    UUID.class,
                    Integer.toString(dotNetBytes.length),
                    "A BSON UUID must contain 16 bytes.");
        }

        byte[] networkBytes = {
                dotNetBytes[3], dotNetBytes[2], dotNetBytes[1], dotNetBytes[0],
                dotNetBytes[5], dotNetBytes[4], dotNetBytes[7], dotNetBytes[6],
                dotNetBytes[8], dotNetBytes[9], dotNetBytes[10], dotNetBytes[11],
                dotNetBytes[12], dotNetBytes[13], dotNetBytes[14], dotNetBytes[15]
        };
        ByteBuffer buffer = ByteBuffer.wrap(networkBytes);
        return new UUID(buffer.getLong(), buffer.getLong());
    }
}
