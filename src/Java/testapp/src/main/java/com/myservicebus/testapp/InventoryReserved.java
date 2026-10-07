package com.myservicebus.testapp;

import java.util.UUID;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@com.myservicebus.MessageUrnName(value = "urn:message:TestApp:InventoryReserved", useDefaultPrefix = false)
@com.myservicebus.EntityName("TestApp:InventoryReserved")
public class InventoryReserved {
    private UUID orderId;
}
