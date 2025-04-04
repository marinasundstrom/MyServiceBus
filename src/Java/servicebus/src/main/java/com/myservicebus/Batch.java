package com.myservicebus;

import com.fasterxml.jackson.annotation.JsonProperty;
import lombok.Data;

@Data
public class Batch<T> {

    @JsonProperty("message")
    private T[] message;
}