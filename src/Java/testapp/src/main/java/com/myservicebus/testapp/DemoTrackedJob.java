package com.myservicebus.testapp;

public record DemoTrackedJob(
        String reportName,
        boolean failFirstAttempt,
        boolean failAlways) {
}
