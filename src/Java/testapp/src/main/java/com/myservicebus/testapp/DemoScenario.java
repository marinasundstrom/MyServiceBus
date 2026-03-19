package com.myservicebus.testapp;

public final class DemoScenario {
    private DemoScenario() {
    }

    public static String createSubmitMessage(String origin, boolean shouldFault) {
        return "submit:" + (shouldFault ? "fault" : "ok") + ":" + origin;
    }

    public static String createRequestMessage(String origin, boolean shouldFault) {
        return "request:" + (shouldFault ? "fault" : "ok") + ":" + origin;
    }

    public static boolean shouldFaultSubmit(String message) {
        return message != null && message.toLowerCase().startsWith("submit:fault:");
    }

    public static boolean shouldFaultRequest(String message) {
        return message != null && message.toLowerCase().startsWith("request:fault:");
    }
}
