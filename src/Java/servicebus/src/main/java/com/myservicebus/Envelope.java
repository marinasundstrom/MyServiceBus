package com.myservicebus;

import com.fasterxml.jackson.annotation.JsonFormat;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import lombok.Data;
import lombok.NoArgsConstructor;
import lombok.AllArgsConstructor;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;

@Data
@NoArgsConstructor
@AllArgsConstructor
@JsonInclude(JsonInclude.Include.NON_NULL)
public class Envelope<T> {

    @JsonProperty("messageId")
    private UUID messageId;

    @JsonProperty("requestId")
    private UUID requestId;

    @JsonProperty("correlationId")
    private UUID correlationId;

    @JsonProperty("conversationId")
    private UUID conversationId;

    @JsonProperty("initiatorId")
    private UUID initiatorId;

    @JsonProperty("sourceAddress")
    private String sourceAddress;

    @JsonProperty("destinationAddress")
    private String destinationAddress;

    @JsonProperty("responseAddress")
    private String responseAddress;

    @JsonProperty("faultAddress")
    private String faultAddress;

    @JsonProperty("expirationTime")
    @JsonFormat(shape = JsonFormat.Shape.STRING, pattern = "yyyy-MM-dd'T'HH:mm:ss.SSSXXX")
    private OffsetDateTime expirationTime;

    @JsonProperty("sentTime")
    @JsonFormat(shape = JsonFormat.Shape.STRING, pattern = "yyyy-MM-dd'T'HH:mm:ss.SSSXXX")
    private OffsetDateTime sentTime;

    @JsonProperty("messageType")
    private List<String> messageType;

    @JsonProperty("message")
    private T message;

    @JsonProperty("headers")
    private Map<String, Object> headers;

    @JsonProperty("host")
    private HostInfo host;

    @JsonProperty("contentType")
    private String contentType;
}