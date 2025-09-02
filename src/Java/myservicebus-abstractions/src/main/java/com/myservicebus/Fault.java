package com.myservicebus;

import com.fasterxml.jackson.annotation.JsonProperty;
import lombok.Data;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

@Data
public class Fault<T> {

    @JsonProperty("message")
    private T message;

    @JsonProperty("exceptions")
    private List<ExceptionInfo> exceptions;

    @JsonProperty("host")
    private HostInfo host;

    @JsonProperty("messageId")
    private UUID messageId;

    @JsonProperty("sentTime")
    private OffsetDateTime sentTime;

    @JsonProperty("faultId")
    private UUID faultId;

    @JsonProperty("conversationId")
    private UUID conversationId;

    @JsonProperty("correlationId")
    private UUID correlationId;

    // add more if needed (headers, etc.)
}
