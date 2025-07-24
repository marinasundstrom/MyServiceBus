package com.myservicebus.contexts;

import java.net.URI;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class SendContextImpl<T> implements SendContext<T> {
    private T message;
    private URI destinationAddress;
}