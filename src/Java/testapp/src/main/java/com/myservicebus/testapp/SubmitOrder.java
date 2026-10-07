package com.myservicebus.testapp;

import java.util.UUID;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@com.myservicebus.MessageUrnName(value = "urn:message:TestApp:SubmitOrder", useDefaultPrefix = false)
@com.myservicebus.EntityName("TestApp:SubmitOrder")
public class SubmitOrder {
    private UUID orderId;
    private String message;
}
