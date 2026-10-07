package com.myservicebus.testapp;

import java.util.UUID;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@com.myservicebus.MessageUrnName(value = "urn:message:TestApp:InventoryReservationRequested", useDefaultPrefix = false)
@com.myservicebus.EntityName("TestApp:InventoryReservationRequested")
public class InventoryReservationRequested {
    private UUID orderId;
}
