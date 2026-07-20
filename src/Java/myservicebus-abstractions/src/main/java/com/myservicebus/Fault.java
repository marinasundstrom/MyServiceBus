package com.myservicebus;

import com.fasterxml.jackson.annotation.JsonProperty;
import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonFormat;
import lombok.Data;

import java.time.OffsetDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

@Data
@JsonIgnoreProperties(ignoreUnknown = true)
public class Fault<T> {

    @JsonProperty("message")
    private T message;

    @JsonProperty("exceptions")
    private List<ExceptionInfo> exceptions = new ArrayList<>();

    @JsonProperty("host")
    private HostInfo host;

    @JsonProperty("faultedMessageId")
    private UUID messageId;

    @JsonProperty("timestamp")
    @JsonFormat(shape = JsonFormat.Shape.STRING)
    private OffsetDateTime sentTime;

    @JsonProperty("faultId")
    private UUID faultId;

    @JsonProperty("conversationId")
    private UUID conversationId;

    @JsonProperty("correlationId")
    private UUID correlationId;
}
