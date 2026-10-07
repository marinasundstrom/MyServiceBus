package com.myservicebus.testapp;

import java.util.UUID;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@com.myservicebus.MessageUrnName(value = "urn:message:TestApp:FulfillmentCompleted", useDefaultPrefix = false)
@com.myservicebus.EntityName("TestApp:FulfillmentCompleted")
public class FulfillmentCompleted {
    private UUID orderId;
}
