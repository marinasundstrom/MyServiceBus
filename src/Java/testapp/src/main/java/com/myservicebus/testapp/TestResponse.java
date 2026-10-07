package com.myservicebus.testapp;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@com.myservicebus.MessageUrnName(value = "urn:message:TestApp:TestResponse", useDefaultPrefix = false)
@com.myservicebus.EntityName("TestApp:TestResponse")
public class TestResponse {
    private String message;
}