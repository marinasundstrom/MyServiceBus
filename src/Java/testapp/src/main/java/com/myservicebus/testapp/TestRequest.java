package com.myservicebus.testapp;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@com.myservicebus.MessageUrnName(value = "urn:message:TestApp:TestRequest", useDefaultPrefix = false)
@com.myservicebus.EntityName("TestApp:TestRequest")
public class TestRequest {
    private String message;
}