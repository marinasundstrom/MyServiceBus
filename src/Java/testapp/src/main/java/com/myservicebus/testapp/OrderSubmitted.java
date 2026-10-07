package com.myservicebus.testapp;

import java.util.UUID;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@com.myservicebus.MessageUrnName(value = "urn:message:TestApp:OrderSubmitted", useDefaultPrefix = false)
@com.myservicebus.EntityName("TestApp:OrderSubmitted")
public class OrderSubmitted {
    private UUID orderId;
    private String replica;
}
