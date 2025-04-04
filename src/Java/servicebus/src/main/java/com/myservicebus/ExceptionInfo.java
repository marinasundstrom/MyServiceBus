package com.myservicebus;

import com.fasterxml.jackson.annotation.JsonProperty;

import lombok.Data;

@Data
public class ExceptionInfo {
    @JsonProperty("exceptionType")
    private String exceptionType;

    @JsonProperty("message")
    private String message;

    @JsonProperty("stackTrace")
    private String stackTrace;

    @JsonProperty("source")
    private String source;

    @JsonProperty("innerException")
    private ExceptionInfo innerException;
}