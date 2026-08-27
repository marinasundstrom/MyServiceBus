package com.myservicebus.serialization;

public enum MessageIntent {
    SEND("Send"),
    PUBLISH("Publish"),
    REPLY("Reply");

    private final String headerValue;

    MessageIntent(String headerValue) {
        this.headerValue = headerValue;
    }

    public String getHeaderValue() {
        return headerValue;
    }
}
