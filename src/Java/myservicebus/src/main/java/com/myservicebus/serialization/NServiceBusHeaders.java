package com.myservicebus.serialization;

public final class NServiceBusHeaders {
    public static final String CONTENT_TYPE = "NServiceBus.ContentType";
    public static final String ENCLOSED_MESSAGE_TYPES = "NServiceBus.EnclosedMessageTypes";
    public static final String MESSAGE_ID = "NServiceBus.MessageId";
    public static final String MESSAGE_INTENT = "NServiceBus.MessageIntent";
    public static final String CORRELATION_ID = "NServiceBus.CorrelationId";
    public static final String CONVERSATION_ID = "NServiceBus.ConversationId";
    public static final String REPLY_TO_ADDRESS = "NServiceBus.ReplyToAddress";
    public static final String RELATED_TO = "NServiceBus.RelatedTo";
    public static final String TIME_SENT = "NServiceBus.TimeSent";

    private NServiceBusHeaders() {
    }
}
