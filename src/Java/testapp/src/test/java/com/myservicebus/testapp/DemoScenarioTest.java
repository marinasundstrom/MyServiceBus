package com.myservicebus.testapp;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

class DemoScenarioTest {
    @Test
    void submitFaultMessagesAreDetected() {
        assertTrue(DemoScenario.shouldFaultSubmit(DemoScenario.createSubmitMessage("java", true)));
        assertFalse(DemoScenario.shouldFaultSubmit(DemoScenario.createSubmitMessage("java", false)));
    }

    @Test
    void requestFaultMessagesAreDetected() {
        assertTrue(DemoScenario.shouldFaultRequest(DemoScenario.createRequestMessage("java", true)));
        assertFalse(DemoScenario.shouldFaultRequest(DemoScenario.createRequestMessage("java", false)));
    }
}
